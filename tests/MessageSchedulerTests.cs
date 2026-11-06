using System;
using System.Collections.Generic;
using Xunit;

namespace NGT.AutoBroadcaster.Tests
{
    public class MessageSchedulerTests
    {
        [Fact]
        public void FiresEachMatchingMessageOnceDuringTheMinute()
        {
            List<string> fired = new List<string>();
            MessageScheduler scheduler = CreateScheduler(fired, SampleMessages());
            DateTime during = new DateTime(2026, 10, 19, 14, 15, 1);

            scheduler.Tick(during);
            scheduler.Tick(during.AddSeconds(20));
            scheduler.Tick(during.AddSeconds(50));

            Assert.Equal(new[] { "Message 01", "Message 02" }, fired);
        }

        [Fact]
        public void DoesNotFireAMessageOutsideItsMinute()
        {
            List<string> fired = new List<string>();
            MessageScheduler scheduler = CreateScheduler(fired, SampleMessages());

            scheduler.Tick(new DateTime(2026, 10, 19, 14, 14, 59));
            scheduler.Tick(new DateTime(2026, 10, 19, 14, 16, 0));

            Assert.Empty(fired);
        }

        [Fact]
        public void FiresAgainOnTheNextHourAndAtMidnight()
        {
            List<string> fired = new List<string>();
            ScheduledMessage[] messages = new[]
            {
                Message("Message 59", 59),
                Message("Message 00", 0)
            };
            MessageScheduler scheduler = CreateScheduler(fired, messages);

            scheduler.Tick(new DateTime(2026, 10, 19, 23, 59, 10));
            scheduler.Tick(new DateTime(2026, 10, 19, 23, 59, 50));
            scheduler.Tick(new DateTime(2026, 10, 20, 0, 0, 5));
            scheduler.Tick(new DateTime(2026, 10, 20, 1, 0, 5));

            Assert.Equal(new[] { "Message 59", "Message 00", "Message 00" }, fired);
        }

        [Fact]
        public void SkipsDisabledMessagesAndEmptySchedules()
        {
            List<string> fired = new List<string>();
            ScheduledMessage[] messages = new[]
            {
                new ScheduledMessage("Message 01", false, 15, DeliveryMethod.Alert, "Hidden", 5),
                Message("Message 02", 15)
            };
            MessageScheduler scheduler = CreateScheduler(fired, messages);

            scheduler.Tick(new DateTime(2026, 10, 19, 14, 15, 0));
            Assert.Equal(new[] { "Message 02" }, fired);

            fired.Clear();
            MessageScheduler empty = CreateScheduler(fired, Array.Empty<ScheduledMessage>());
            empty.Tick(new DateTime(2026, 10, 19, 14, 15, 0));
            Assert.Empty(fired);
        }

        private static MessageScheduler CreateScheduler(List<string> fired, IReadOnlyList<ScheduledMessage> messages)
        {
            return new MessageScheduler(
                () => messages,
                message => fired.Add(message.Id),
                _ => { });
        }

        private static ScheduledMessage[] SampleMessages()
        {
            return new[]
            {
                Message("Message 01", 15),
                Message("Message 02", 15),
                Message("Message 03", 45)
            };
        }

        private static ScheduledMessage Message(string id, int minute)
        {
            return new ScheduledMessage(id, true, minute, DeliveryMethod.Alert, id + " text", 5);
        }
    }
}
