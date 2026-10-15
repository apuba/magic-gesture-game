using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagicMotion.Core
{
    [DisallowMultipleComponent]
    public sealed class SceneController : MonoBehaviour
    {
        public bool IsLoading { get; private set; }

        public void LoadScene(string sceneName, Action completed = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name is required.", nameof(sceneName));
            }

            if (!IsLoading)
            {
                StartCoroutine(LoadSceneRoutine(sceneName, completed));
            }
        }

        private IEnumerator LoadSceneRoutine(string sceneName, Action completed)
        {
            IsLoading = true;
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                IsLoading = false;
                Debug.LogError($"[MagicMotion] Scene could not be loaded: {sceneName}");
                yield break;
            }

            yield return operation;
            IsLoading = false;
            completed?.Invoke();
        }
    }
}

