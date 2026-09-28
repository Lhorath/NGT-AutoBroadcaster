using System;
using BepInEx;
using UnityEngine;

namespace NGT.AutoBroadcaster
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AutoBroadcasterPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "NGT_Autobroadcaster";
        public const string PluginName = "Nerdy Gamer Tools AutoBroadcaster";
        public const string PluginVersion = "2.0.0";

        private MessageConfigService _configService;
        private MessageScheduler _scheduler;
        private BroadcastDispatcher _dispatcher;
        private float _nextSchedulerTick;

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
            if (_configService == null || _scheduler == null)
            {
                return;
            }

            _configService.ReloadIfRequested();

            // Dedicated-server only. Accidentally placing the DLL on a normal client does nothing.
            if (!Application.isBatchMode || ZNet.instance == null || !ZNet.instance.IsServer())
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < _nextSchedulerTick)
            {
                return;
            }

            _nextSchedulerTick = now + 0.5f;
            _scheduler.Tick(DateTime.Now);
        }

        private void OnDestroy()
        {
            if (_configService != null)
            {
                _configService.Dispose();
                _configService = null;
            }
        }
    }
}
