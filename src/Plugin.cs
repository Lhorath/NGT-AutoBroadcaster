using System;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;

namespace NGT.AutoBroadcaster
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AutoBroadcasterPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "NGT_Autobroadcaster";
        public const string PluginName = "Nerdy Gamer Tools AutoBroadcaster";
        public const string PluginVersion = "3.2.0";

        private MessageConfigService _configService;
        private MessageScheduler _scheduler;
        private BroadcastDispatcher _dispatcher;
        private float _nextSchedulerTick;
        private bool _reportedReady;
        private bool _reportedDisabled;

        private void Awake()
        {
            _configService = new MessageConfigService(
                Config,
                message => Logger.LogInfo(message),
                message => Logger.LogWarning(message));

            _configService.Initialize();

            _dispatcher = new BroadcastDispatcher(
                this,
                message => Logger.LogInfo(message),
                message => Logger.LogWarning(message),
                () => _configService.DebugLogging);

            _scheduler = new MessageScheduler(
                () => _configService.IsEnabled ? _configService.Messages : Array.Empty<ScheduledMessage>(),
                message => _dispatcher.Dispatch(message),
                message =>
                {
                    if (_configService.DebugLogging)
                    {
                        Logger.LogInfo(message);
                    }
                });

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. Waiting for the dedicated server network to become ready.");
        }

        private void Update()
        {
            if (_configService == null || _scheduler == null || _dispatcher == null)
            {
                return;
            }

            _configService.ReloadIfRequested();

            // Dedicated-server build only. A normal client and a listen host both report IsDedicated() == false.
            if (ZNet.instance == null || !ZNet.instance.IsServer() || !ZNet.instance.IsDedicated())
            {
                return;
            }

            if (!_reportedReady)
            {
                _reportedReady = true;
                ServerPlayerListAnchor.Apply(message => Logger.LogWarning(message));
                LogReady();
            }

            if (!_configService.IsEnabled)
            {
                if (!_reportedDisabled)
                {
                    _reportedDisabled = true;
                    Logger.LogInfo("AutoBroadcaster is loaded, but EnableBroadcasting is false. No messages will be sent.");
                }

                return;
            }

            _reportedDisabled = false;

            float now = Time.realtimeSinceStartup;
            if (now < _nextSchedulerTick)
            {
                return;
            }

            _nextSchedulerTick = now + 0.5f;
            _scheduler.Tick(DateTime.Now);
        }

        private void LogReady()
        {
            int enabled = 0;
            IReadOnlyList<ScheduledMessage> messages = _configService.Messages;
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Enabled)
                {
                    enabled++;
                }
            }

            if (enabled == 0)
            {
                Logger.LogInfo("AutoBroadcaster ready on the dedicated server, but no enabled messages are configured. Vanilla clients do not need this mod.");
                return;
            }

            Logger.LogInfo("AutoBroadcaster ready on the dedicated server. " + enabled + " enabled message(s). Vanilla clients do not need this mod.");
            for (int i = 0; i < messages.Count; i++)
            {
                ScheduledMessage message = messages[i];
                if (!message.Enabled)
                {
                    continue;
                }

                Logger.LogInfo("Scheduled " + message.Id + " at minute " + message.MinuteOfHour.ToString("00") + " (" + message.Method + ").");
            }
        }

        private void OnDestroy()
        {
            if (_dispatcher != null)
            {
                _dispatcher.Release();
                _dispatcher = null;
            }

            if (_configService != null)
            {
                _configService.Dispose();
                _configService = null;
            }
        }
    }
}
