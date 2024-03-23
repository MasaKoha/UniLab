using UnityEngine;

namespace UniLab.Common
{
    public abstract class SingletonMonoBehaviour<TMonoBehaviourClass> : MonoBehaviour where TMonoBehaviourClass : MonoBehaviour
    {
        private static TMonoBehaviourClass _instance;
        public static TMonoBehaviourClass Instance => Initialize();

        private static TMonoBehaviourClass Initialize()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var instance = FindObjectOfType<TMonoBehaviourClass>();
            if (instance != null)
            {
                _instance = instance;
                return _instance;
            }

            var gameObjectName = typeof(TMonoBehaviourClass).ToString();
            return _instance = new GameObject($"[{gameObjectName}]").AddComponent<TMonoBehaviourClass>();
        }

        protected void SetDonDestroyOnLoad()
        {
            DontDestroyOnLoad(_instance.gameObject);
        }
    }
}