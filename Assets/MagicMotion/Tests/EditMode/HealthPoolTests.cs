using System;
using MagicMotion.Combat;
using NUnit.Framework;

namespace MagicMotion.Tests
{
    public sealed class HealthPoolTests
    {
        [Test]
        public void ApplyDamage_ClampsAtZero()
        {
            var health = new HealthPool(100f);

            var applied = health.ApplyDamage(140f);

            Assert.That(applied, Is.EqualTo(100f));
            Assert.That(health.Current, Is.Zero);
            Assert.That(health.IsDepleted, Is.True);
        }

        [Test]
        public void ApplyDamage_IgnoresNonPositiveDamage()
        {
            var health = new HealthPool(100f);

            Assert.That(health.ApplyDamage(0f), Is.Zero);
            Assert.That(health.ApplyDamage(-10f), Is.Zero);
            Assert.That(health.Current, Is.EqualTo(100f));
        }

        [Test]
        public void Constructor_RejectsInvalidMaximum()
        {
            Assert.That(() => new HealthPool(0f), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
