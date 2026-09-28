using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Splatform;
using UnityEngine;

namespace NGT.AutoBroadcaster
{
    internal sealed class BroadcastDispatcher
    {
        private const string ChatSenderName = "Server";
        private const string ChatSenderId = "NGTAutoBroadcaster_0";

        private readonly MonoBehaviour _coroutineHost;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private readonly Func<bool> _debugLogging;
        private readonly MethodInfo _sendPlayerListMethod;
        private bool _chatWarningLogged;

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

            try
            {
                _sendPlayerListMethod = typeof(ZNet).GetMethod(
                    "SendPlayerList",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
            }
            catch
            {
                _sendPlayerListMethod = null;
            }
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
                    SendChat(message.Text);
                    break;
                case DeliveryMethod.Both:
                    SendAlert(message);
                    SendChat(message.Text);
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
                BroadcastCenterAlert(message.Text);
                if (message.AlertDurationSeconds > 2)
                {
                    _coroutineHost.StartCoroutine(RepeatAlert(message.Text, message.AlertDurationSeconds));
                }

                _logInfo("AutoBroadcaster alert: " + message.Text);
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
                BroadcastCenterAlert(text);
            }
        }

        private static void BroadcastCenterAlert(string text)
        {
            if (ZRoutedRpc.instance == null)
            {
                return;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody,
                "ShowMessage",
                (int)MessageHud.MessageType.Center,
                text);
        }

        private void SendChat(string text)
        {
            try
            {
                ZNet znet = ZNet.instance;
                if (znet == null || !znet.IsServer() || ZRoutedRpc.instance == null)
                {
                    return;
                }

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

                if (recipients.Count == 0)
                {
                    return;
                }

                ZPackage temporaryPlayerList = BuildPlayerList(znet.GetPlayerList(), true);
                UserInfo sender = new UserInfo();
                sender.Name = ChatSenderName;
                sender.UserId = new PlatformUserID(ChatSenderId);

                try
                {
                    for (int i = 0; i < recipients.Count; i++)
                    {
                        ZNetPeer peer = recipients[i];
                        if (peer.m_rpc == null || !peer.m_rpc.IsConnected())
                        {
                            continue;
                        }

                        // Vanilla clients only display chat from identities present in the player list.
                        // Briefly add a synthetic, non-player server identity, send the chat line, then restore the real list.
                        peer.m_rpc.Invoke("PlayerList", temporaryPlayerList);
                        ZRoutedRpc.instance.InvokeRoutedRPC(
                            peer.m_uid,
                            "ChatMessage",
                            Vector3.zero,
                            (int)Talker.Type.Normal,
                            sender,
                            text);
                    }
                }
                finally
                {
                    RestorePlayerList(znet, recipients);
                }

                _logInfo("AutoBroadcaster chat: " + text);
            }
            catch (Exception ex)
            {
                if (!_chatWarningLogged || _debugLogging())
                {
                    _chatWarningLogged = true;
                    _logWarning("AutoBroadcaster chat failed. Alert delivery is unaffected: " + Describe(ex));
                }
            }
        }

        private ZPackage BuildPlayerList(List<ZNet.PlayerInfo> players, bool includeSyntheticServer)
        {
            ZPackage package = new ZPackage();
            package.Write(players.Count + (includeSyntheticServer ? 1 : 0));

            for (int i = 0; i < players.Count; i++)
            {
                ZNet.PlayerInfo player = players[i];
                WritePlayerEntry(
                    package,
                    player.m_name,
                    player.m_characterID,
                    player.m_userInfo.m_id.ToString(),
                    player.m_userInfo.m_displayName,
                    player.m_userInfo.m_serverAssignedDisplayName,
                    player.m_userInfo.m_playfabId,
                    player.m_publicPosition,
                    player.m_position);
            }

            if (includeSyntheticServer)
            {
                WritePlayerEntry(
                    package,
                    ChatSenderName,
                    ZDOID.None,
                    ChatSenderId,
                    ChatSenderName,
                    string.Empty,
                    string.Empty,
                    false,
                    Vector3.zero);
            }

            return package;
        }

        private void RestorePlayerList(ZNet znet, List<ZNetPeer> recipients)
        {
            if (_sendPlayerListMethod != null)
            {
                try
                {
                    _sendPlayerListMethod.Invoke(znet, null);
                    return;
                }
                catch (Exception ex)
                {
                    if (_debugLogging())
                    {
                        _logWarning("Vanilla SendPlayerList restore failed; using fallback: " + Describe(ex));
                    }
                }
            }

            try
            {
                ZPackage realList = BuildPlayerList(znet.GetPlayerList(), false);
                for (int i = 0; i < recipients.Count; i++)
                {
                    ZNetPeer peer = recipients[i];
                    if (peer.m_rpc != null && peer.m_rpc.IsConnected())
                    {
                        peer.m_rpc.Invoke("PlayerList", realList);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_debugLogging())
                {
                    _logWarning("Player-list fallback restore failed: " + Describe(ex));
                }
            }
        }

        private static void WritePlayerEntry(
            ZPackage package,
            string name,
            ZDOID characterId,
            string platformId,
            string displayName,
            string serverAssignedDisplayName,
            string playfabId,
            bool publicPosition,
            Vector3 position)
        {
            package.Write(name ?? string.Empty);
            package.Write(characterId);
            package.Write(platformId ?? string.Empty);
            package.Write(displayName ?? string.Empty);
            package.Write(serverAssignedDisplayName ?? string.Empty);
            package.Write(playfabId ?? string.Empty);
            package.Write(publicPosition);
            if (publicPosition)
            {
                package.Write(position);
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
