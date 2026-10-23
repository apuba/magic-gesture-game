using System;
using System.Collections.Generic;
using MagicMotion.Core;
using MagicMotion.Gesture;
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
            GameManager.Instance.Events.Subscribe<RuneReleased>(HandleRuneReleased);
        }

        // The four elemental spells still need their own projectiles and hit
        // feedback; until that art exists the release reuses the palm strike
        // effect so a cast is visible on device and the routing can be verified.
        private void HandleRuneReleased(RuneReleased message)
        {
            Debug.Log($"[MagicMotion.Rune] released element={message.Element} hand={message.Hand}");
            PalmStrikeEffectFactory.Create(message.Hand);
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
            GameManager.Instance.Events.Unsubscribe<RuneReleased>(HandleRuneReleased);
            }
        }
    }
}

