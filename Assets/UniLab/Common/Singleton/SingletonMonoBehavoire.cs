using System;
using UnityEngine;

namespace UniLab.Common
{
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour, IDisposable where T : MonoBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                _instance = FindFirstObjectByType<T>();

                // 既存のインスタンスが見つかった場合は新規生成しない
                if (_instance != null)
                {
                    return _instance;
                }

                // 既存がなければ新規生成
                var singletonObject = new GameObject(typeof(T).Name);
                _instance = singletonObject.AddComponent<T>();

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }

            OnAwake();
        }

        protected virtual void OnAwake()
        {
        }

        protected void SetDontDestroyOnLoad()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void Dispose()
        {
            _instance = null;
            DestroyImmediate(gameObject);
            OnDispose();
        }

        protected virtual void OnDispose()
        {
        }
    }
}