using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NGT.AutoBroadcaster
{
    internal sealed class BroadcastDispatcher
    {
        private readonly MonoBehaviour _coroutineHost;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private readonly Func<bool> _debugLogging;
        private readonly MethodInfo _sendPlayerListMethod;
        private bool _anchorWarningLogged;
        private bool _missingSendPlayerListLogged;

        internal BroadcastDispatcher(
            MonoBehaviour coroutineHost,
            Action<string> logInfo,
            Action<string> logWarning,
            Func<bool> debugLogging)
        {
            _coroutineHost = coroutineHost;
            _logInfo = logInfo;
            _logWarning = logWarning;
            _debugLogging = debugLogging;

            _sendPlayerListMethod = typeof(ZNet).GetMethod(
                "SendPlayerList",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
        }

        internal void Release()
        {
            ServerPlayerListAnchor.IncludeAnnouncer = false;
            ServerPlayerListAnchor.Remove();
        }

        internal void Dispatch(ScheduledMessage message)
        {
            if (message == null || ZNet.instance == null || !ZNet.instance.IsServer())
            {
                return;
            }

            switch (message.Method)
            {
                case DeliveryMethod.Alert:
                    SendAlert(message);
                    break;
                case DeliveryMethod.Chat:
                    SendChat(message.Text, true);
                    break;
                case DeliveryMethod.Both:
                    SendAlert(message);
                    SendChat(message.Text, false);
                    break;
                default:
                    _logWarning("Unknown delivery method for " + message.Id + ".");
                    break;
            }
        }

        private void SendAlert(ScheduledMessage message)
        {
            try
            {
                int recipients = CountReadyPeers();
                if (recipients == 0)
                {
                    _logInfo("AutoBroadcaster alert skipped (no connected players): " + message.Text);
                    return;
                }

                if (!BroadcastCenterAlert(message.Text))
                {
                    _logWarning("AutoBroadcaster alert failed: the routed RPC service is not ready.");
                    return;
                }

                // Valheim 1.0.20 fades a center ShowMessage out over about four seconds.
                // Longer MessageTime values are kept visible by sending the same alert again.
                if (message.AlertDurationSeconds > 2)
                {
                    _coroutineHost.StartCoroutine(RepeatAlert(message.Text, message.AlertDurationSeconds));
                }

                _logInfo("AutoBroadcaster alert (" + recipients + " player(s)): " + message.Text);
            }
            catch (Exception ex)
            {
                _logWarning("AutoBroadcaster alert failed: " + Describe(ex));
            }
        }

        private IEnumerator RepeatAlert(string text, int seconds)
        {
            int refreshes = Math.Max(0, Mathf.CeilToInt(seconds / 2f) - 1);
            for (int i = 0; i < refreshes; i++)
            {
                yield return new WaitForSecondsRealtime(2f);
                if (ZNet.instance == null || !ZNet.instance.IsServer() || CountReadyPeers() == 0)
                {
                    yield break;
                }

                BroadcastCenterAlert(text);
            }
        }

        private static bool BroadcastCenterAlert(string text)
        {
            if (ZRoutedRpc.instance == null)
            {
                return false;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody,
                "ShowMessage",
                (int)MessageHud.MessageType.Center,
                text);
            return true;
        }

        private void SendChat(string text, bool centerFallback)
        {
            try
            {
                ZNet znet = ZNet.instance;
                if (znet == null || !znet.IsServer() || ZRoutedRpc.instance == null)
                {
                    _logWarning("AutoBroadcaster chat failed: the dedicated server network is not ready.");
                    return;
                }

                List<ZNetPeer> recipients = CollectRecipients(znet);
                if (recipients.Count == 0)
                {
                    _logInfo("AutoBroadcaster chat skipped (no connected players): " + text);
                    return;
                }

                if (!ServerPlayerListAnchor.IsApplied)
                {
                    SendUnanchoredChat(text, centerFallback, recipients.Count);
                    return;
                }

                int sent = 0;
                ServerPlayerListAnchor.IncludeAnnouncer = true;
                try
                {
                    if (!TryInvokeSendPlayerList())
                    {
                        SendManualPlayerList(znet, recipients, true);
                    }

                    // The outbound package already contains the announcer. Take it back out of the
                    // live list immediately so the server player count, and the "server full" check,
                    // are not increased by a non-player identity.
                    ServerPlayerListAnchor.RemoveFromLiveList(znet);

                    UserInfo sender = new UserInfo();
                    sender.Name = ServerPlayerListAnchor.AnnouncerName;
                    sender.UserId = ServerPlayerListAnchor.AnnouncerId;

                    for (int i = 0; i < recipients.Count; i++)
                    {
                        ZNetPeer peer = recipients[i];
                        if (peer == null || peer.m_rpc == null || !peer.m_rpc.IsConnected())
                        {
                            continue;
                        }

                        ZRoutedRpc.instance.InvokeRoutedRPC(
                            peer.m_uid,
                            "ChatMessage",
                            Vector3.zero,
                            (int)Talker.Type.Normal,
                            sender,
                            text);
                        sent++;
                    }
                }
                finally
                {
                    ServerPlayerListAnchor.IncludeAnnouncer = false;
                    if (!TryInvokeSendPlayerList())
                    {
                        SendManualPlayerList(znet, recipients, false);
                    }
                }

                if (sent == 0)
                {
                    _logInfo("AutoBroadcaster chat skipped (no connected players): " + text);
                    return;
                }

                _logInfo("AutoBroadcaster chat (" + sent + " player(s)): " + text);
            }
            catch (Exception ex)
            {
                _logWarning("AutoBroadcaster chat failed. Alert delivery is unaffected: " + Describe(ex));
            }
        }

        private void SendUnanchoredChat(string text, bool centerFallback, int recipients)
        {
            if (!_anchorWarningLogged)
            {
                _anchorWarningLogged = true;
                _logWarning("AutoBroadcaster could not hold the Server chat identity in the player list. Chat lines use a center alert so they are still visible.");
            }

            if (!centerFallback)
            {
                _logWarning("AutoBroadcaster chat was not sent for: " + text);
                return;
            }

            if (!BroadcastCenterAlert(text))
            {
                _logWarning("AutoBroadcaster chat fallback alert failed: the routed RPC service is not ready.");
                return;
            }

            _logInfo("AutoBroadcaster chat fallback alert (" + recipients + " player(s)): " + text);
        }

        private bool TryInvokeSendPlayerList()
        {
            if (_sendPlayerListMethod == null || ZNet.instance == null)
            {
                if (_sendPlayerListMethod == null && !_missingSendPlayerListLogged)
                {
                    _missingSendPlayerListLogged = true;
                    _logWarning("AutoBroadcaster could not find ZNet.SendPlayerList. Using the manual player list.");
                }

                return false;
            }

            try
            {
                _sendPlayerListMethod.Invoke(ZNet.instance, null);
                return true;
            }
            catch (Exception ex)
            {
                if (_debugLogging())
                {
                    _logWarning("Vanilla SendPlayerList failed; using the manual player list: " + Describe(ex));
                }

                return false;
            }
        }

        private static List<ZNetPeer> CollectRecipients(ZNet znet)
        {
            List<ZNetPeer> recipients = new List<ZNetPeer>();
            List<ZNetPeer> peers = znet.GetPeers();
            for (int i = 0; i < peers.Count; i++)
            {
                ZNetPeer peer = peers[i];
                if (peer != null && peer.IsReady() && peer.m_rpc != null && peer.m_rpc.IsConnected())
                {
                    recipients.Add(peer);
                }
            }

            return recipients;
        }

        private static int CountReadyPeers()
        {
            ZNet znet = ZNet.instance;
            if (znet == null)
            {
                return 0;
            }

            return CollectRecipients(znet).Count;
        }

        private void SendManualPlayerList(ZNet znet, List<ZNetPeer> recipients, bool includeAnnouncer)
        {
            ZPackage package = BuildPlayerList(znet.GetPlayerList(), includeAnnouncer);
            for (int i = 0; i < recipients.Count; i++)
            {
                ZNetPeer peer = recipients[i];
                if (peer != null && peer.m_rpc != null && peer.m_rpc.IsConnected())
                {
                    peer.m_rpc.Invoke("PlayerList", package);
                }
            }
        }

        private static ZPackage BuildPlayerList(List<ZNet.PlayerInfo> players, bool includeAnnouncer)
        {
            // Field order matches ZNet.RPC_PlayerList on Valheim 1.0.20:
            // count, then name, character ZDOID, platform id, display name,
            // server-assigned display name, PlayFab id, public position, optional position.
            List<ZNet.PlayerInfo> entries = new List<ZNet.PlayerInfo>(players.Count + 1);
            bool seenAnnouncer = false;
            for (int i = 0; i < players.Count; i++)
            {
                ZNet.PlayerInfo player = players[i];
                if (player.m_userInfo.m_id == ServerPlayerListAnchor.AnnouncerId)
                {
                    seenAnnouncer = true;
                    if (!includeAnnouncer)
                    {
                        continue;
                    }
                }

                entries.Add(player);
            }

            if (includeAnnouncer && !seenAnnouncer)
            {
                entries.Add(ServerPlayerListAnchor.CreateInfo());
            }

            ZPackage package = new ZPackage();
            package.Write(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                WritePlayerEntry(package, entries[i]);
            }

            return package;
        }

        private static void WritePlayerEntry(ZPackage package, ZNet.PlayerInfo player)
        {
            package.Write(player.m_name ?? string.Empty);
            package.Write(player.m_characterID);
            package.Write(player.m_userInfo.m_id.ToString() ?? string.Empty);
            package.Write(player.m_userInfo.m_displayName ?? string.Empty);
            package.Write(player.m_userInfo.m_serverAssignedDisplayName ?? string.Empty);
            package.Write(player.m_userInfo.m_playfabId ?? string.Empty);
            package.Write(player.m_publicPosition);
            if (player.m_publicPosition)
            {
                package.Write(player.m_position);
            }
        }

        private static string Describe(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException && current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current.GetType().Name + ": " + current.Message;
        }
    }
}
