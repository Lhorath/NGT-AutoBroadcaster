using System;

namespace NGT.AutoBroadcaster
{
    internal enum DeliveryMethod
    {
        Alert,
        Chat,
        Both
    }

    internal sealed class ScheduledMessage
    {
        internal string Id { get; private set; }
        internal bool Enabled { get; private set; }
        internal int MinuteOfHour { get; private set; }
        internal DeliveryMethod Method { get; private set; }
        internal string Text { get; private set; }
        internal int AlertDurationSeconds { get; private set; }

        internal ScheduledMessage(
            string id,
            bool enabled,
            int minuteOfHour,
            DeliveryMethod method,
            string text,
            int alertDurationSeconds)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Message id is required.", "id");
            if (minuteOfHour < 0 || minuteOfHour > 59) throw new ArgumentOutOfRangeException("minuteOfHour");
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Message text is required.", "text");
            if (alertDurationSeconds < 1) throw new ArgumentOutOfRangeException("alertDurationSeconds");

            Id = id.Trim();
            Enabled = enabled;
            MinuteOfHour = minuteOfHour;
            Method = method;
            Text = text.Trim();
            AlertDurationSeconds = alertDurationSeconds;
        }
    }
}
