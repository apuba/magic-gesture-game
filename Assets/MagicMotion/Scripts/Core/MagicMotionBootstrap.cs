using MagicMotion.Config;
using MagicMotion.Camera;
using MagicMotion.Vision;
using MagicMotion.Gesture;
using MagicMotion.Motion;
using MagicMotion.Skill;
using MagicMotion.Combat;
using MagicMotion.Enemy;
using MagicMotion.UI;
using MagicMotion.Calibration;
using UnityEngine;

namespace MagicMotion.Core
{
    public static class MagicMotionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (GameManager.Instance != null)
            {
                return;
            }

            var root = new GameObject("MagicMotion.Systems");
            var gameManager = root.AddComponent<GameManager>();
            root.AddComponent<SceneController>();
            var configManager = root.AddComponent<ConfigManager>();
            root.AddComponent<CameraManager>();
            root.AddComponent<MediaPipeHandTrackingProvider>();
            root.AddComponent<VisionPipeline>();
            root.AddComponent<GestureRecognizer>();
            root.AddComponent<MotionAnalyzer>();
            root.AddComponent<SkillManager>();
            root.AddComponent<SkillExecutor>();
            root.AddComponent<CombatManager>();
            root.AddComponent<BattleDemoBootstrap>();
            root.AddComponent<ExperienceFlowController>();
            root.AddComponent<DebugHud>();
            root.AddComponent<HandTrackingDebugView>();
            root.AddComponent<GestureDataRecorder>();

            try
            {
                configManager.Load();
                gameManager.MarkReady();
                Debug.Log("[MagicMotion] Core systems initialized.");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}

