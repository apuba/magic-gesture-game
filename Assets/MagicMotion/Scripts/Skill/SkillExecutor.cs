using System;
using System.Collections.Generic;
using MagicMotion.Core;
using MagicMotion.VFX;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Skill
{
    [DisallowMultipleComponent]
    public sealed class SkillExecutor : MonoBehaviour
    {
        private Dictionary<string, Action<Handedness>> effects;

        private void Start()
        {
            effects = new Dictionary<string, Action<Handedness>>(StringComparer.OrdinalIgnoreCase)
            {
                ["PalmStrike"] = hand => PalmStrikeEffectFactory.Create(hand)
            };
            GameManager.Instance.Events.Subscribe<SkillCast>(HandleSkillCast);
        }

        private void HandleSkillCast(SkillCast message)
        {
            if (string.IsNullOrWhiteSpace(message.Vfx) || !effects.TryGetValue(message.Vfx, out var execute))
            {
                Debug.LogError($"[MagicMotion.Skill] No effect executor is registered for '{message.Vfx}'.");
                return;
            }

            execute(message.Hand);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Events.Unsubscribe<SkillCast>(HandleSkillCast);
            }
        }
    }
}

