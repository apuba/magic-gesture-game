using System.Linq;
using MagicMotion.Calibration;
using NUnit.Framework;

namespace MagicMotion.Tests
{
    public sealed class GestureRecordingPlanTests
    {
        [Test]
        public void CreatePushCalibrationPlan_HasThirtyPositiveAndFifteenNegativeTrials()
        {
            var plan = GestureRecordingPlan.CreatePushCalibrationPlan();

            Assert.That(plan.Count, Is.EqualTo(45));
            Assert.That(plan.Count(trial => trial.IsPositive), Is.EqualTo(30));
            Assert.That(plan.Count(trial => !trial.IsPositive), Is.EqualTo(15));
        }

        [Test]
        public void CreatePushCalibrationPlan_CoversEveryDistanceAndZoneTwice()
        {
            var plan = GestureRecordingPlan.CreatePushCalibrationPlan();
            var distances = new[] { 0.5f, 1.0f, 1.5f };
            var zones = new[] { "center", "top", "bottom", "left", "right" };

            foreach (var distance in distances)
            {
                foreach (var zone in zones)
                {
                    Assert.That(
                        plan.Count(trial => trial.IsPositive &&
                                            trial.distanceMeters == distance &&
                                            trial.zone == zone),
                        Is.EqualTo(2),
                        $"Missing coverage for distance={distance}, zone={zone}");
                }
            }
        }

        [Test]
        public void CreatePushCalibrationPlan_CoversEachNegativeActionThreeTimes()
        {
            var plan = GestureRecordingPlan.CreatePushCalibrationPlan();
            var actions = new[]
            {
                "open_palm_hold",
                "horizontal_move",
                "vertical_move",
                "retract",
                "side_or_fist_forward"
            };

            foreach (var action in actions)
            {
                Assert.That(
                    plan.Count(trial => !trial.IsPositive && trial.action == action),
                    Is.EqualTo(3),
                    $"Missing negative action coverage for {action}");
            }
        }
    }
}
