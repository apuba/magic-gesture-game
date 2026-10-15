using MagicMotion.Vision;
using NUnit.Framework;

namespace MagicMotion.Tests
{
    public sealed class FrameRateGateTests
    {
        [Test]
        public void ShouldProcess_LimitsFramesToConfiguredRate()
        {
            var gate = new FrameRateGate(10);

            Assert.That(gate.ShouldProcess(0.00), Is.True);
            Assert.That(gate.ShouldProcess(0.05), Is.False);
            Assert.That(gate.ShouldProcess(0.10), Is.True);
        }

        [Test]
        public void Reset_AllowsImmediateFrame()
        {
            var gate = new FrameRateGate(15);
            gate.ShouldProcess(2.0);
            gate.Reset();

            Assert.That(gate.ShouldProcess(2.01), Is.True);
        }

        [Test]
        public void Constructor_RejectsNonPositiveRate()
        {
            Assert.That(
                () => new FrameRateGate(0),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
