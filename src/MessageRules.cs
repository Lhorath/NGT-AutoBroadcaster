using System;

namespace NGT.AutoBroadcaster
{
    internal static class MessageRules
    {
        internal static bool TryCreate(
            string id,
            bool enabled,
            string text,
            string methodText,
            int minute,
            int durationSeconds,
            out ScheduledMessage message,
            out string error)
        {
            message = null;
            error = string.Empty;

            if (minute < 0 || minute > 59)
            {
                error = "ScheduledMinuteOfHour must be between 0 and 59.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(methodText) ||
                !Enum.TryParse(methodText.Trim(), true, out DeliveryMethod method))
            {
                error = "MessageType must be Alert, Chat, or Both.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "MessageText cannot be empty.";
                return false;
            }

            if (durationSeconds < 1)
            {
                error = "MessageTime must be at least 1 second.";
                return false;
            }

            message = new ScheduledMessage(id, enabled, minute, method, text, durationSeconds);
            return true;
        }
    }
}
