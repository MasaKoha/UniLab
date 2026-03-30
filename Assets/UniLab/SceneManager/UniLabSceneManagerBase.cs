using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UniLab.Common;
using UniLab.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UniLab.Scene
{
    /// <summary>
    /// Singleton scene manager base. Handles scene loading lifecycle and implements ISceneManager for DI consumers.
    /// </summary>
    public abstract class UniLabSceneManagerBase<T> : SingletonMonoBehaviour<T>, ISceneManager
        where T : MonoBehaviour
    {
        private readonly Stack<SceneParameterBase> _sceneHistory = new();
        private IDisposable _updateDisposable;

        /// <summary>
        /// True after ExecuteBootSequence has completed.
        /// </summary>
        public bool IsBoot { get; private set; }

        /// <summary>Plays the fade-in animation before leaving a scene.</summary>
        protected abstract UniTask FadeInAsync();

        /// <summary>Plays the fade-out animation after entering a scene.</summary>
        protected abstract UniTask FadeOutAsync();

        protected override void OnAwake()
        {
            SetDontDestroyOnLoad();
            BackKeyInputManager.Instance.OnPressBackKey
                .Subscribe(_ => GoBack())
                .AddTo(destroyCancellationToken);
        }

        private void Update()
        {
#if UNITY_ANDROID
            if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            GoBack();
#endif
        }

        private void GoBack()
        {
#if UNITY_ANDROID
            if (_sceneHistory.Count <= 1 || BackKeyInputManager.Instance.IsBlocked)
            {
                return;
            }

            BackToPreviousScene();
#endif
        }

        /// <summary>
        /// Starts loading the next scene. Fire-and-forget wrapper over LoadSceneAsync.
        /// </summary>
        public void GoToNextScene(SceneParameterBase sceneParameter, bool addToHistory = false)
        {
            LoadSceneAsync(sceneParameter, addToHistory).Forget();
        }

        /// <summary>
        /// Overload that preserves backward compatibility with callers that pass LoadSceneMode explicitly.
        /// </summary>
        public void GoToNextScene(SceneParameterBase sceneParameter, bool addToHistory, LoadSceneMode mode)
        {
            if (addToHistory)
            {
                _sceneHistory.Push(sceneParameter);
            }

            LoadScene(sceneParameter.SceneName.ToString(), sceneParameter, CancellationToken.None, mode).Forget();
        }

        /// <summary>
        /// Pops the history stack and returns to the previous scene.
        /// </summary>
        public void BackToPreviousScene()
        {
            if (_sceneHistory.Count <= 1)
            {
                return;
            }

            _sceneHistory.Pop();
            var sceneParameter = _sceneHistory.Peek();
            LoadScene(sceneParameter.SceneName.ToString(), sceneParameter, CancellationToken.None).Forget();
        }

        /// <summary>
        /// Clears the scene navigation history stack.
        /// </summary>
        public void ClearHistory()
        {
            _sceneHistory.Clear();
        }

        /// <summary>
        /// Loads the scene described by <paramref name="parameter"/> and awaits the full lifecycle sequence.
        /// Pushes to history when <paramref name="addToHistory"/> is true.
        /// </summary>
        public UniTask LoadSceneAsync(
            SceneParameterBase parameter,
            bool addToHistory = false,
            CancellationToken cancellationToken = default)
        {
            if (addToHistory)
            {
                _sceneHistory.Push(parameter);
            }

            return LoadScene(parameter.SceneName.ToString(), parameter, cancellationToken);
        }

        /// <summary>
        /// Returns the current scene's parameter cast to <typeparamref name="TParameter"/>.
        /// Returns null and logs an error if the cast fails.
        /// </summary>
        public TParameter GetCurrentSceneParameter<TParameter>() where TParameter : SceneParameterBase
        {
            if (_sceneHistory.Count == 0)
            {
                return null;
            }

            var currentSceneParameter = _sceneHistory.Peek();
            if (currentSceneParameter is TParameter parameter)
            {
                return parameter;
            }

            Debug.LogError($"Current scene parameter is not of type {typeof(TParameter)}");
            return null;
        }

        private async UniTask LoadScene(
            string sceneName,
            SceneParameterBase sceneParameter,
            CancellationToken cancellationToken,
            LoadSceneMode mode = LoadSceneMode.Single)
        {
            BackKeyInputManager.Instance.SetBlock(true);
            var previousScene = SceneManager.GetActiveScene();
            foreach (var rootGameObject in previousScene.GetRootGameObjects())
            {
                var previousComponent = rootGameObject.GetComponent<SceneMainBase>();
                if (previousComponent == null)
                {
                    continue;
                }

                await previousComponent.TransitionAsync().AttachExternalCancellation(cancellationToken);
                await FadeInAsync().AttachExternalCancellation(cancellationToken);
                previousComponent.Leave();
                break;
            }

            SceneManager.LoadScene(sceneName, mode);
            // Wait until the new scene is fully active before proceeding with its lifecycle.
            await UniTask.WaitUntil(
                () => SceneManager.GetActiveScene().name == sceneName,
                cancellationToken: cancellationToken);

            var currentScene = SceneManager.GetActiveScene();
            foreach (var gameObjectInstance in currentScene.GetRootGameObjects())
            {
                var component = gameObjectInstance.GetComponent<SceneMainBase>();
                if (component == null)
                {
                    continue;
                }

                component.SetParameter(sceneParameter);
                component.Setup();
                _ = component.Initialize();
                await component.PreEnterAsync().AttachExternalCancellation(cancellationToken);
                await FadeOutAsync().AttachExternalCancellation(cancellationToken);
                component.Enter();
                break;
            }

            BackKeyInputManager.Instance.SetBlock(false);
        }

        /// <summary>
        /// Runs the boot sequence for the initial scene. No-op if already booted.
        /// </summary>
        public async UniTask ExecuteBootSequence()
        {
            if (IsBoot)
            {
                return;
            }

            var currentScene = SceneManager.GetActiveScene();
            foreach (var rootGameObject in currentScene.GetRootGameObjects())
            {
                var currentComponent = rootGameObject.GetComponent<SceneMainBase>();
                if (currentComponent == null)
                {
                    continue;
                }

                currentComponent.Setup();
                var initialized = currentComponent.Initialize();
                if (initialized)
                {
                    break;
                }

                await currentComponent.PreEnterAsync();
                await FadeOutAsync();
                currentComponent.Enter();
                await currentComponent.TransitionAsync();

                break;
            }

            IsBoot = true;
        }

        protected override void OnDispose()
        {
            _updateDisposable?.Dispose();
            _updateDisposable = null;
        }
    }
}
