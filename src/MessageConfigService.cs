using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;

namespace NGT.AutoBroadcaster
{
    internal sealed class MessageConfigService : IDisposable
    {
        private readonly ConfigFile _configFile;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private readonly object _reloadLock = new object();

        private ConfigEntry<bool> _enabledEntry;
        private ConfigEntry<int> _countEntry;
        private ConfigEntry<bool> _debugEntry;
        private FileSystemWatcher _watcher;
        private bool _reloadRequested;
        private DateTime _reloadAfterUtc;
        private ScheduledMessage[] _messages = Array.Empty<ScheduledMessage>();

        internal bool IsEnabled { get; private set; }
        internal bool DebugLogging { get; private set; }
        internal IReadOnlyList<ScheduledMessage> Messages { get { return _messages; } }

        internal MessageConfigService(ConfigFile configFile, Action<string> logInfo, Action<string> logWarning)
        {
            _configFile = configFile;
            _logInfo = logInfo;
            _logWarning = logWarning;
        }

        internal void Initialize()
        {
            _enabledEntry = _configFile.Bind(
                "Default Settings",
                "EnableBroadcasting",
                true,
                "Enable or disable all scheduled automatic broadcasts.");

            _countEntry = _configFile.Bind(
                "Default Settings",
                "AutoBroadcastCount",
                3,
                "Number of [Message XX] sections to load.");

            _debugEntry = _configFile.Bind(
                "Default Settings",
                "DebugLogging",
                false,
                "Write extra scheduler and delivery information to the BepInEx log.");

            LoadMessages();
            StartWatcher();
        }

        internal void ReloadIfRequested()
        {
            bool shouldReload = false;
            lock (_reloadLock)
            {
                if (_reloadRequested && DateTime.UtcNow >= _reloadAfterUtc)
                {
                    _reloadRequested = false;
                    shouldReload = true;
                }
            }

            if (!shouldReload)
            {
                return;
            }

            try
            {
                _configFile.Reload();
                LoadMessages();
                _logInfo("AutoBroadcaster config reloaded. Loaded " + _messages.Length + " valid message section(s).");
            }
            catch (Exception ex)
            {
                _logWarning("AutoBroadcaster config reload failed: " + ex.Message);
            }
        }

        private void StartWatcher()
        {
            string path = _configFile.ConfigFilePath;
            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                _logWarning("Config path could not be resolved. Live reload is disabled.");
                return;
            }

            _watcher = new FileSystemWatcher(directory, fileName);
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
            _watcher.Changed += OnConfigChanged;
            _watcher.Created += OnConfigChanged;
            _watcher.Renamed += OnConfigChanged;
            _watcher.EnableRaisingEvents = true;
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs args)
        {
            lock (_reloadLock)
            {
                _reloadRequested = true;
                _reloadAfterUtc = DateTime.UtcNow.AddMilliseconds(500);
            }
        }

        private void LoadMessages()
        {
            IsEnabled = _enabledEntry != null && _enabledEntry.Value;
            DebugLogging = _debugEntry != null && _debugEntry.Value;

            int count = _countEntry == null ? 0 : Math.Max(0, _countEntry.Value);
            List<ScheduledMessage> parsed = new List<ScheduledMessage>(count);

            for (int i = 1; i <= count; i++)
            {
                string section = "Message " + i.ToString("00");
                ScheduledMessage message;
                string error;
                if (TryReadMessage(section, out message, out error))
                {
                    parsed.Add(message);
                }
                else
                {
                    _logWarning("Skipping [" + section + "]: " + error);
                }
            }

            _messages = parsed
                .OrderBy(message => message.MinuteOfHour)
                .ThenBy(message => message.Id, StringComparer.Ordinal)
                .ToArray();
        }

        private bool TryReadMessage(string section, out ScheduledMessage message, out string error)
        {
            message = null;
            error = string.Empty;

            ConfigEntry<bool> enabled = _configFile.Bind(
                section,
                "Enabled",
                true,
                "Enable or disable this individual broadcast.");

            ConfigEntry<string> text = _configFile.Bind(
                section,
                "MessageText",
                DefaultText(section),
                "Message displayed to players. Valheim rich-text tags may be used where the target UI supports them.");

            ConfigEntry<string> method = _configFile.Bind(
                section,
                "MessageType",
                DefaultMethod(section),
                "Alert, Chat, or Both.");

            ConfigEntry<int> minute = _configFile.Bind(
                section,
                "ScheduledMinuteOfHour",
                DefaultMinute(section),
                "Minute of every real-world server hour when this message is sent (0-59).");

            ConfigEntry<int> duration = _configFile.Bind(
                section,
                "MessageTime",
                5,
                "Requested center-alert duration in seconds. Values above 2 are maintained by refreshing the vanilla alert.");

            if (minute.Value < 0 || minute.Value > 59)
            {
                error = "ScheduledMinuteOfHour must be between 0 and 59.";
                return false;
            }

            DeliveryMethod parsedMethod;
            if (!Enum.TryParse(method.Value.Trim(), true, out parsedMethod))
            {
                error = "MessageType must be Alert, Chat, or Both.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(text.Value))
            {
                error = "MessageText cannot be empty.";
                return false;
            }

            if (duration.Value < 1)
            {
                error = "MessageTime must be at least 1 second.";
                return false;
            }

            message = new ScheduledMessage(
                section,
                enabled.Value,
                minute.Value,
                parsedMethod,
                text.Value,
                duration.Value);
            return true;
        }

        private static string DefaultText(string section)
        {
            switch (section)
            {
                case "Message 01": return "Welcome to the server! Be respectful and have fun.";
                case "Message 02": return "Join our Discord to be part of the community.";
                case "Message 03": return "Thank you for playing!";
                default: return "Edit this message.";
            }
        }

        private static string DefaultMethod(string section)
        {
            switch (section)
            {
                case "Message 01": return "Alert";
                case "Message 02": return "Chat";
                case "Message 03": return "Both";
                default: return "Alert";
            }
        }

        private static int DefaultMinute(string section)
        {
            switch (section)
            {
                case "Message 01": return 15;
                case "Message 02": return 30;
                case "Message 03": return 45;
                default: return 0;
            }
        }

        public void Dispose()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnConfigChanged;
                _watcher.Created -= OnConfigChanged;
                _watcher.Renamed -= OnConfigChanged;
                _watcher.Dispose();
                _watcher = null;
            }
        }
    }
}
