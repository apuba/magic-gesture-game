using MagicMotion.Gesture;
using MagicMotion.Motion;
using MagicMotion.Skill;
using MagicMotion.Vision;
using NUnit.Framework;

namespace MagicMotion.Tests
{
    public sealed class SkillInputMatcherTests
    {
        [Test]
        public void Matches_AcceptsCompletePalmStrikeInput()
        {
            var input = Snapshot(GestureType.OpenPalm, PalmDirection.Forward, MotionType.Push, 1.0, 1.1);

            Assert.That(SkillInputMatcher.Matches(SkillTestFactory.PalmStrike(), input), Is.True);
        }

        [Test]
        public void Matches_RejectsWrongPalmDirection()
        {
            var input = Snapshot(GestureType.OpenPalm, PalmDirection.Left, MotionType.Push, 1.0, 1.1);

            Assert.That(SkillInputMatcher.Matches(SkillTestFactory.PalmStrike(), input), Is.False);
        }

        [Test]
        public void Matches_RejectsStalePalmObservation()
        {
            var input = Snapshot(GestureType.OpenPalm, PalmDirection.Forward, MotionType.Push, 1.0, 1.5);

            Assert.That(SkillInputMatcher.Matches(SkillTestFactory.PalmStrike(), input), Is.False);
        }

        private static SkillInputSnapshot Snapshot(
            GestureType gesture,
            PalmDirection palm,
            MotionType motion,
            double palmTime,
            double motionTime)
        {
            return new SkillInputSnapshot(
                Handedness.Right,
                gesture,
                0.95f,
                palm,
                palmTime,
                motion,
                0.95f,
                motionTime);
        }
    }
}

