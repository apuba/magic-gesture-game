using MagicMotion.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagicMotion.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BattleDemoBootstrap : MonoBehaviour
    {
        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "BattleScene" ||
                UnityEngine.Object.FindFirstObjectByType<EnemyHealth>() != null)
            {
                return;
            }

            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "StoneDemon.Placeholder";
            target.transform.position = new Vector3(0f, 1f, 4f);
            target.transform.localScale = new Vector3(1.5f, 2f, 1.2f);

            // Gameplay first: the target stays damageable even if the placeholder
            // material cannot be built on a stripped player build.
            target.AddComponent<EnemyHealth>().Configure("StoneDemon", 100f);

            var renderer = target.GetComponent<Renderer>();
            renderer.material = RuntimeMaterialFactory.CreateLit(
                new Color(0.28f, 0.25f, 0.20f),
                Color.black);
            GameManager.Instance.ChangeState(GameState.Battle);
        }
    }
}

