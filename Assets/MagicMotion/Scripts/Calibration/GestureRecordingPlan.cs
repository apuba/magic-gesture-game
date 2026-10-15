using System;
using System.Collections.Generic;

namespace MagicMotion.Calibration
{
    [Serializable]
    public sealed class GestureRecordingTrial
    {
        public int id;
        public string category;
        public string action;
        public string zone;
        public float distanceMeters;
        public int repetition;

        public bool IsPositive => string.Equals(category, "positive", StringComparison.Ordinal);

        public string Instruction
        {
            get
            {
                if (IsPositive)
                {
                    return $"距离约 {distanceMeters:0.0} 米，只伸出一只手，放在{GestureRecordingPlan.ZoneName(zone)}，完成一次自然推掌";
                }

                return $"负样本：{GestureRecordingPlan.ActionName(action)}";
            }
        }
    }

    public static class GestureRecordingPlan
    {
        // Display names live here so the recorder can speak the same wording it
        // shows on screen.
        public static string ZoneName(string value)
        {
            return value switch
            {
                "center" => "画面中心",
                "top" => "画面上方",
                "bottom" => "画面下方",
                "left" => "画面左侧",
                "right" => "画面右侧",
                _ => value
            };
        }

        public static string ActionName(string value)
        {
            return value switch
            {
                "open_palm_hold" => "只伸出一只手，张开手掌保持静止",
                "horizontal_move" => "只伸出一只手，左右移动，不要向前推",
                "vertical_move" => "只伸出一只手，上下移动，不要向前推",
                "retract" => "只伸出一只手，向身体方向收回",
                "side_or_fist_forward" => "只伸出一只手，用侧掌或握拳向前伸出",
                _ => value
            };
        }

        // Short cues for the giant on-screen text: one glance, one word.
        public static string ZoneShortName(string value)
        {
            return value switch
            {
                "center" => "中心",
                "top" => "上方",
                "bottom" => "下方",
                "left" => "左侧",
                "right" => "右侧",
                _ => value
            };
        }

        public static string ActionShortName(string value)
        {
            return value switch
            {
                "open_palm_hold" => "静止",
                "horizontal_move" => "横移",
                "vertical_move" => "竖移",
                "retract" => "收回",
                "side_or_fist_forward" => "握拳",
                _ => value
            };
        }

        private static readonly float[] PositiveDistances = { 0.5f, 1.0f, 1.5f };
        private static readonly string[] PositiveZones = { "center", "top", "bottom", "left", "right" };
        private static readonly string[] NegativeActions =
        {
            "open_palm_hold",
            "horizontal_move",
            "vertical_move",
            "retract",
            "side_or_fist_forward"
        };

        public static List<GestureRecordingTrial> CreatePushCalibrationPlan()
        {
            var trials = new List<GestureRecordingTrial>(45);
            var id = 1;
            for (var distanceIndex = 0; distanceIndex < PositiveDistances.Length; distanceIndex++)
            {
                for (var zoneIndex = 0; zoneIndex < PositiveZones.Length; zoneIndex++)
                {
                    for (var repetition = 1; repetition <= 2; repetition++)
                    {
                        trials.Add(new GestureRecordingTrial
                        {
                            id = id++,
                            category = "positive",
                            action = "push",
                            zone = PositiveZones[zoneIndex],
                            distanceMeters = PositiveDistances[distanceIndex],
                            repetition = repetition
                        });
                    }
                }
            }

            for (var actionIndex = 0; actionIndex < NegativeActions.Length; actionIndex++)
            {
                for (var repetition = 1; repetition <= 3; repetition++)
                {
                    trials.Add(new GestureRecordingTrial
                    {
                        id = id++,
                        category = "negative",
                        action = NegativeActions[actionIndex],
                        zone = "free",
                        distanceMeters = 1.0f,
                        repetition = repetition
                    });
                }
            }

            return trials;
        }
    }
}
