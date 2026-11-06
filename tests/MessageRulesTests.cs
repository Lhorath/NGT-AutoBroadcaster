using Xunit;

namespace NGT.AutoBroadcaster.Tests
{
    public class MessageRulesTests
    {
        [Theory]
        [InlineData("Alert", "Alert")]
        [InlineData("chat", "Chat")]
        [InlineData("BOTH", "Both")]
        public void AcceptsLegacyMessageTypeSpellings(string method, string expected)
        {
            ScheduledMessage message;
            string error;
            bool created = MessageRules.TryCreate("Message 01", true, " Hello ", method, 0, 5, out message, out error);

            Assert.True(created, error);
            Assert.Equal(expected, message.Method.ToString());
            Assert.Equal("Hello", message.Text);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void RejectsMinuteOutsideTheHour()
        {
            ScheduledMessage message;
            string error;
            bool created = MessageRules.TryCreate("Message 01", true, "Hello", "Alert", 60, 5, out message, out error);

            Assert.False(created);
            Assert.Null(message);
            Assert.Contains("0 and 59", error);
        }

        [Fact]
        public void RejectsEmptyTextAndUnknownTypeAndShortDuration()
        {
            ScheduledMessage message;
            string error;

            Assert.False(MessageRules.TryCreate("Message 01", true, "   ", "Alert", 1, 5, out message, out error));
            Assert.Contains("MessageText", error);

            Assert.False(MessageRules.TryCreate("Message 01", true, "Hello", "Shout", 1, 5, out message, out error));
            Assert.Contains("MessageType", error);

            Assert.False(MessageRules.TryCreate("Message 01", true, "Hello", "Alert", 1, 0, out message, out error));
            Assert.Contains("MessageTime", error);
        }

        [Fact]
        public void DisabledMessageIsStillAValidScheduleEntry()
        {
            ScheduledMessage message;
            string error;
            bool created = MessageRules.TryCreate("Message 04", false, "Later", "Alert", 45, 1, out message, out error);

            Assert.True(created, error);
            Assert.False(message.Enabled);
            Assert.Equal(45, message.MinuteOfHour);
        }
    }
}
