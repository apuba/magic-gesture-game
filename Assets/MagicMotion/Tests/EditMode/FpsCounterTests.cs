using System;
using MagicMotion.UI;
using NUnit.Framework;

namespace MagicMotion.Tests
{
    public sealed class FpsCounterTests
    {
        [Test]
        public void AddFrame_UpdatesAtConfiguredInterval()
        {
            var counter = new FpsCounter(0.5d);

            for (var index = 0; index < 4; index++)
            {
                Assert.That(counter.AddFrame(0.1d), Is.False);
            }

            Assert.That(counter.AddFrame(0.1d), Is.True);
            Assert.That(counter.Current, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void Constructor_RejectsInvalidInterval()
        {
            Assert.That(() => new FpsCounter(0d), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
