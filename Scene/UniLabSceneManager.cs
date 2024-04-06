using System.Collections.Generic;
using UniLab.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.Scene
{
    public sealed class UniLabSceneManager : Singleton<UniLabSceneManager>
    {
        private readonly Stack<SceneParameterBase> _sceneHistory = new();

        public void Initialize()
        {
        }

        public void GoToNextScene<T>(T sceneParameter, LoadSceneMode mode = LoadSceneMode.Single, bool isPushHistory = true)
            where T : SceneParameterBase, new()
        {
            if (isPushHistory)
            {
                _sceneHistory.Push(sceneParameter);
            }

            SceneManager.sceneLoaded += (scene, _) => OnSceneLoaded(scene, sceneParameter);
            SceneManager.LoadScene(sceneParameter.SceneName, mode);
            SceneManager.sceneLoaded -= (scene, _) => OnSceneLoaded(scene, sceneParameter);
        }

        public void BackToPreviousScene()
        {
            if (_sceneHistory.Count == 0)
            {
                return;
            }

            _sceneHistory.Pop();
            SceneManager.LoadScene(_sceneHistory.Peek().SceneName);
        }

        public void BackToPreviousScene<T>(T sceneParameter) where T : SceneParameterBase, new()
        {
            if (_sceneHistory.Count == 0)
            {
                return;
            }

            if (sceneParameter.SceneName != _sceneHistory.Peek().SceneName)
            {
                Debug.LogWarning("The parameter is no same Pop scene with sceneParameter.");
            }

            _sceneHistory.Pop();
            SceneManager.sceneLoaded += (scene, _) => OnSceneLoaded(scene, sceneParameter);
            SceneManager.LoadScene(_sceneHistory.Peek().SceneName);
            SceneManager.sceneLoaded -= (scene, _) => OnSceneLoaded(scene, sceneParameter);
        }

        private void OnSceneLoaded<T>(UnityEngine.SceneManagement.Scene scene, T sceneParameter) where T : SceneParameterBase, new()
        {
            foreach (var gameObjectInstance in scene.GetRootGameObjects())
            {
                var component = gameObjectInstance.GetComponent<SceneMainBase<T>>();
                if (component != null)
                {
                    component.InitializeAsync(sceneParameter);
                }
            }
        }
    }
}