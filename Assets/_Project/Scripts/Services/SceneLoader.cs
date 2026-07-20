using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OblastZero.Core
{
    public interface ISceneLoader : IService
    {
        void LoadSceneAdditive(string sceneName, Action onComplete = null);
        void UnloadScene(string sceneName, Action onComplete = null);
        void ReplaceActiveScene(string sceneName, Action onComplete = null);
        bool IsLoading { get; }
    }

    /// <summary>
    /// Async scene loader using additive scenes. The Bootstrap scene is never unloaded;
    /// gameplay scenes load additively on top and unload when their state exits.
    ///
    /// All scene operations route through this single point so we have one place to add
    /// loading screens, fade transitions, and async hooks.
    /// </summary>
    public class SceneLoader : MonoBehaviour, ISceneLoader
    {
        public bool IsLoading { get; private set; }

        public void LoadSceneAdditive(string sceneName, Action onComplete = null)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Already loading a scene. Queuing not yet supported. Aborting load of {sceneName}.");
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneLoader] LoadSceneAdditive called with null/empty scene name.");
                return;
            }

            StartCoroutine(LoadSceneRoutine(sceneName, onComplete));
        }

        public void UnloadScene(string sceneName, Action onComplete = null)
        {
            if (!SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                Debug.LogWarning($"[SceneLoader] Tried to unload scene {sceneName} but it isn't loaded.");
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(UnloadSceneRoutine(sceneName, onComplete));
        }

        public void ReplaceActiveScene(string sceneName, Action onComplete = null)
        {
            // Common pattern: unload everything except _Bootstrap, then load target.
            StartCoroutine(ReplaceActiveSceneRoutine(sceneName, onComplete));
        }

        private IEnumerator LoadSceneRoutine(string sceneName, Action onComplete)
        {
            IsLoading = true;
            Debug.Log($"[SceneLoader] Loading {sceneName} (additive)...");

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] LoadSceneAsync returned null for {sceneName}. Is it in Build Settings?");
                IsLoading = false;
                yield break;
            }

            while (!op.isDone)
            {
                EventBus.Raise(new SceneLoadProgressEvent { SceneName = sceneName, Progress = op.progress });
                yield return null;
            }

            var loaded = SceneManager.GetSceneByName(sceneName);
            SceneManager.SetActiveScene(loaded);

            Debug.Log($"[SceneLoader] {sceneName} loaded and set active.");
            IsLoading = false;
            onComplete?.Invoke();
            EventBus.Raise(new SceneLoadedEvent { SceneName = sceneName });
        }

        private IEnumerator UnloadSceneRoutine(string sceneName, Action onComplete)
        {
            IsLoading = true;
            Debug.Log($"[SceneLoader] Unloading {sceneName}...");

            var op = SceneManager.UnloadSceneAsync(sceneName);
            while (!op.isDone)
            {
                yield return null;
            }

            Debug.Log($"[SceneLoader] {sceneName} unloaded.");
            IsLoading = false;
            onComplete?.Invoke();
            EventBus.Raise(new SceneUnloadedEvent { SceneName = sceneName });
        }

        private IEnumerator ReplaceActiveSceneRoutine(string sceneName, Action onComplete)
        {
            IsLoading = true;

            // Unload every loaded scene that isn't _Bootstrap.
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name != "_Bootstrap" && scene.isLoaded)
                {
                    Debug.Log($"[SceneLoader] Replace: unloading {scene.name}");
                    var op = SceneManager.UnloadSceneAsync(scene);
                    while (op != null && !op.isDone) yield return null;
                }
            }

            // Load target additively.
            var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (loadOp == null)
            {
                Debug.LogError($"[SceneLoader] LoadSceneAsync returned null for {sceneName}. Check Build Settings.");
                IsLoading = false;
                yield break;
            }

            while (!loadOp.isDone)
            {
                EventBus.Raise(new SceneLoadProgressEvent { SceneName = sceneName, Progress = loadOp.progress });
                yield return null;
            }

            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
            Debug.Log($"[SceneLoader] Replace complete — active scene is now {sceneName}.");

            IsLoading = false;
            onComplete?.Invoke();
            EventBus.Raise(new SceneLoadedEvent { SceneName = sceneName });
        }
    }
}
