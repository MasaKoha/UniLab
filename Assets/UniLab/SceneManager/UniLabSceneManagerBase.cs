using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UniLab.Common;
using UniLab.Common.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UniLab.Scene
{
    public abstract class UniLabSceneManagerBase<T> : SingletonMonoBehaviour<T> where T : MonoBehaviour
    {
        private readonly Stack<SceneParameterBase> _sceneHistory = new();
        public bool IsBoot { get; private set; }
        private IDisposable _updateDisposable;
        protected abstract UniTask FadeInAsync();
        protected abstract UniTask FadeOutAsync();

        protected override void OnAwake()
        {
            SetDontDestroyOnLoad();
            BackKeyInputManager.Instance.OnPressBackKey
                .Subscribe(_ => GoBack())
                .AddTo(this);
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

        public void GoToNextScene(SceneParameterBase sceneParameter, bool addToHistory = false, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (addToHistory)
            {
                _sceneHistory.Push(sceneParameter);
            }

            LoadScene(sceneParameter.SceneName.ToString(), sceneParameter, mode).Forget();
        }

        public void BackToPreviousScene()
        {
            if (_sceneHistory.Count <= 1)
            {
                return;
            }

            _sceneHistory.Pop();
            var sceneParameter = _sceneHistory.Peek();
            LoadScene(sceneParameter.SceneName.ToString(), sceneParameter).Forget();
        }

        public void ClearHistory()
        {
            _sceneHistory.Clear();
        }

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

        private async UniTask LoadScene(string sceneName, SceneParameterBase sceneParameter, LoadSceneMode mode = LoadSceneMode.Single)
        {
            BackKeyInputManager.Instance.SetBlock(true);
            var prevScene = SceneManager.GetActiveScene();
            foreach (var rootGameObject in prevScene.GetRootGameObjects())
            {
                var prevComponent = rootGameObject.GetComponent<SceneMainBase>();
                if (prevComponent == null)
                {
                    continue;
                }

                await prevComponent.TransitionAsync();
                await FadeInAsync();
                prevComponent.Leave();
                break;
            }

            SceneManager.LoadScene(sceneName, mode);
            // すぐシーンが読み込まれるわけではないので、シーンが確実に読み込まれるまで待機する
            await UniTask.WaitUntil(() => SceneManager.GetActiveScene().name == sceneName);
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
                await component.PreEnterAsync();
                await FadeOutAsync();
                component.Enter();
                break;
            }

            BackKeyInputManager.Instance.SetBlock(false);
        }

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