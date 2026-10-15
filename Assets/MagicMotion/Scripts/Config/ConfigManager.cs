using System;
using UnityEngine;

namespace MagicMotion.Config
{
    [DisallowMultipleComponent]
    public sealed class ConfigManager : MonoBehaviour
    {
        [SerializeField] private string gameConfigResource = "Config/GameConfig";
        [SerializeField] private string motionConfigResource = "Config/MotionConfig";
        [SerializeField] private string skillConfigResource = "Config/Skills";

        public GameConfig Game { get; private set; }
        public MotionConfig Motion { get; private set; }
        public SkillCatalogConfig Skills { get; private set; }
        public bool IsLoaded => Game != null && Motion != null && Skills != null;

        public void Load()
        {
            Game = LoadResource<GameConfig>(gameConfigResource);
            Motion = LoadResource<MotionConfig>(motionConfigResource);
            Skills = LoadResource<SkillCatalogConfig>(skillConfigResource);

            Application.targetFrameRate = Game.targetFrameRate;
        }

        private static T LoadResource<T>(string resourcePath) where T : class
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Missing configuration resource: Resources/{resourcePath}.json");
            }

            var value = JsonUtility.FromJson<T>(asset.text);
            if (value == null)
            {
                throw new InvalidOperationException($"Configuration is invalid: {resourcePath}.json");
            }

            return value;
        }
    }
}

