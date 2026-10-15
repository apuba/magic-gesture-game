using MagicMotion.Core;
using NUnit.Framework;

namespace MagicMotion.Tests
{
    public sealed class EventBusTests
    {
        private readonly struct TestEvent
        {
            public TestEvent(int value) => Value = value;
            public int Value { get; }
        }

        [Test]
        public void Publish_NotifiesSubscriber()
        {
            var bus = new EventBus();
            var received = 0;
            bus.Subscribe<TestEvent>(message => received = message.Value);

            bus.Publish(new TestEvent(42));

            Assert.That(received, Is.EqualTo(42));
        }

        [Test]
        public void Unsubscribe_StopsNotifications()
        {
            var bus = new EventBus();
            var calls = 0;
            System.Action<TestEvent> handler = _ => calls++;
            bus.Subscribe(handler);
            bus.Unsubscribe(handler);

            bus.Publish(new TestEvent(1));

            Assert.That(calls, Is.Zero);
        }
    }
}

