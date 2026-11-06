using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Splatform;
using UnityEngine;

namespace NGT.AutoBroadcaster
{
    /// <summary>
    /// Adds a non-player "Server" identity to the player list the dedicated server is about
    /// to send. Valheim 1.0 clients drop a chat line unless that sender is in the list while
    /// the line is displayed. The identity is removed from the server's live list before the
    /// method returns so it does not change the connected-player count.
    /// </summary>
    internal static class ServerPlayerListAnchor
    {
        internal const string AnnouncerName = "Server";

        internal static readonly PlatformUserID AnnouncerId = new PlatformUserID("Dedicated", "Server");

        private static Harmony _harmony;
        private static Action<string> _logWarning;
        private static bool _faultLogged;

        internal static bool IncludeAnnouncer { get; set; }

        internal static bool IsApplied { get; private set; }

        internal static bool Apply(Action<string> logWarning)
        {
            _logWarning = logWarning;
            if (IsApplied)
            {
                return true;
            }

            MethodInfo method = AccessTools.Method(typeof(ZNet), "UpdatePlayerList");
            if (method == null)
            {
                logWarning("AutoBroadcaster could not find ZNet.UpdatePlayerList. Chat will use a one-shot player list and a center-alert backup.");
                return false;
            }

            try
            {
                _harmony = new Harmony(AutoBroadcasterPlugin.PluginGuid + ".playerlist");
                _harmony.Patch(method, postfix: new HarmonyMethod(typeof(ServerPlayerListAnchor), nameof(AddAnnouncer)));
                IsApplied = true;
                return true;
            }
            catch (Exception ex)
            {
                logWarning("AutoBroadcaster could not anchor chat to the player list: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        internal static void Remove()
        {
            IncludeAnnouncer = false;
            if (_harmony != null)
            {
                try
                {
                    _harmony.UnpatchSelf();
                }
                catch (Exception ex)
                {
                    if (_logWarning != null && !_faultLogged)
                    {
                        _faultLogged = true;
                        _logWarning("AutoBroadcaster could not remove the player-list anchor: " + ex.Message);
                    }
                }

                _harmony = null;
            }

            IsApplied = false;
        }

        internal static void RemoveFromLiveList(ZNet znet)
        {
            if (znet == null)
            {
                return;
            }

            List<ZNet.PlayerInfo> players = znet.GetPlayerList();
            if (players == null)
            {
                return;
            }

            for (int i = players.Count - 1; i >= 0; i--)
            {
                if (players[i].m_userInfo.m_id == AnnouncerId)
                {
                    players.RemoveAt(i);
                }
            }
        }

        internal static ZNet.PlayerInfo CreateInfo()
        {
            return new ZNet.PlayerInfo
            {
                m_name = AnnouncerName,
                m_characterID = ZDOID.None,
                m_publicPosition = false,
                m_position = Vector3.zero,
                m_userInfo = new ZNet.CrossNetworkUserInfo
                {
                    m_id = AnnouncerId,
                    m_displayName = AnnouncerName,
                    m_serverAssignedDisplayName = AnnouncerName,
                    m_playfabId = string.Empty
                }
            };
        }

        private static void AddAnnouncer(ZNet __instance)
        {
            if (!IncludeAnnouncer || __instance == null)
            {
                return;
            }

            try
            {
                List<ZNet.PlayerInfo> players = __instance.GetPlayerList();
                if (players == null)
                {
                    return;
                }

                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].m_userInfo.m_id == AnnouncerId)
                    {
                        return;
                    }
                }

                players.Add(CreateInfo());
            }
            catch (Exception ex)
            {
                if (!_faultLogged && _logWarning != null)
                {
                    _faultLogged = true;
                    _logWarning("AutoBroadcaster player-list anchor failed: " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }
    }
}
