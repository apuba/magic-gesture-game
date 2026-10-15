using MagicMotion.Camera;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagicMotion.Core
{
    [DisallowMultipleComponent]
    public sealed class ExperienceFlowController : MonoBehaviour
    {
        private CameraManager cameraManager;
        private bool bootTransitionScheduled;

        private void Awake()
        {
            cameraManager = GetComponent<CameraManager>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            ApplyScene(SceneManager.GetActiveScene().name);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyScene(scene.name);
        }

        private void ApplyScene(string sceneName)
        {
            switch (sceneName)
            {
                case "BootScene":
                    GameManager.Instance.ChangeState(GameState.Boot);
                    cameraManager.StopCamera();
                    if (!bootTransitionScheduled)
                    {
                        bootTransitionScheduled = true;
                        StartCoroutine(EnterMvpBattle());
                    }
                    break;
                case "TrainingScene":
                    GameManager.Instance.ChangeState(GameState.Training);
                    cameraManager.StartCamera();
                    break;
                case "BattleScene":
                    GameManager.Instance.ChangeState(GameState.Battle);
                    cameraManager.StartCamera();
                    break;
                default:
                    cameraManager.StopCamera();
                    break;
            }
        }

        private IEnumerator EnterMvpBattle()
        {
            // Let all persistent systems finish Start before replacing BootScene.
            yield return null;

            var operation = SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
            if (operation == null)
            {
                bootTransitionScheduled = false;
                Debug.LogError("[MagicMotion] Failed to enter the MVP battle scene.");
                yield break;
            }

            yield return operation;
            bootTransitionScheduled = false;
        }
    }
}

