using System;
using System.Collections.Generic;

namespace NGT.AutoBroadcaster
{
    internal sealed class MessageScheduler
    {
        private readonly Func<IReadOnlyList<ScheduledMessage>> _getMessages;
        private readonly Action<ScheduledMessage> _dispatch;
        private readonly Action<string> _logDebug;
        private readonly HashSet<string> _fired = new HashSet<string>(StringComparer.Ordinal);
        private string _activeHourKey = string.Empty;

        internal MessageScheduler(
            Func<IReadOnlyList<ScheduledMessage>> getMessages,
            Action<ScheduledMessage> dispatch,
            Action<string> logDebug)
        {
            _getMessages = getMessages;
            _dispatch = dispatch;
            _logDebug = logDebug;
        }

        internal void Tick(DateTime localNow)
        {
            string hourKey = localNow.ToString("yyyyMMddHH");
            if (!string.Equals(_activeHourKey, hourKey, StringComparison.Ordinal))
            {
                _activeHourKey = hourKey;
                _fired.Clear();
                _logDebug("Scheduler entered hour " + hourKey + ".");
            }

            IReadOnlyList<ScheduledMessage> messages = _getMessages();
            for (int i = 0; i < messages.Count; i++)
            {
                ScheduledMessage message = messages[i];
                if (!message.Enabled || message.MinuteOfHour != localNow.Minute)
                {
                    continue;
                }

                string executionKey = hourKey + "|" + message.Id;
                if (_fired.Add(executionKey))
                {
                    _dispatch(message);
                }
            }
        }
    }
}
