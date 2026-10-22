using System;

namespace MagicMotion.Config
{
    [Serializable]
    public sealed class GameConfig
    {
        public string applicationName = "Magic Motion";
        public string trainingScene = "TrainingScene";
        public string battleScene = "BattleScene";
        public int targetFrameRate = 30;
        public int visionFrameRate = 30;
        public int cameraWidth = 1280;
        public int cameraHeight = 720;
        public int cameraFrameRate = 30;
        public bool preferFrontCamera = true;
    }
}

