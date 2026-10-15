using MagicMotion.Skill;
using NUnit.Framework;

namespace MagicMotion.Tests
{
    public sealed class SkillRuntimeStateTests
    {
        [Test]
        public void Cooldown_PreventsCastUntilExpired()
        {
            var runtime = new SkillRuntimeState(SkillTestFactory.PalmStrike(1f));
            Assert.That(runtime.TryPrepare(0.0), Is.True);
            Assert.That(runtime.TryCast(0.1), Is.True);
            runtime.EnterCooldown(0.1);

            Assert.That(runtime.TryCast(0.9), Is.False);
            Assert.That(runtime.TryCast(1.1), Is.True);
        }

        [Test]
        public void Cancel_ReturnsPreparedSkillToIdle()
        {
            var runtime = new SkillRuntimeState(SkillTestFactory.PalmStrike());
            runtime.TryPrepare(0.0);

            runtime.Cancel();

            Assert.That(runtime.State, Is.EqualTo(SkillState.Idle));
        }

        [Test]
        public void Refresh_ReportsCooldownCompletionOnce()
        {
            var runtime = new SkillRuntimeState(SkillTestFactory.PalmStrike(1f));
            runtime.EnterCooldown(2.0);

            Assert.That(runtime.Refresh(2.9), Is.False);
            Assert.That(runtime.Refresh(3.0), Is.True);
            Assert.That(runtime.Refresh(3.1), Is.False);
            Assert.That(runtime.State, Is.EqualTo(SkillState.Idle));
        }
    }
}
