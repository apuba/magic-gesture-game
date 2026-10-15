using MagicMotion.Core;
using UnityEngine;

namespace MagicMotion.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatManager : MonoBehaviour
    {
        private int defeatedEnemies;

        private void Start()
        {
            GameManager.Instance.Events.Subscribe<EnemyDefeated>(HandleEnemyDefeated);
        }

        private void HandleEnemyDefeated(EnemyDefeated message)
        {
            defeatedEnemies++;
            GameManager.Instance.Events.Publish(new BattleCompleted(defeatedEnemies));
            GameManager.Instance.ChangeState(GameState.Result);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Events.Unsubscribe<EnemyDefeated>(HandleEnemyDefeated);
            }
        }
    }
}

