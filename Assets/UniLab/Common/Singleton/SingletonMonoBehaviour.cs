using System;
using UnityEngine;

namespace UniLab.Common
{
    /// <summary>
    /// Generic singleton base for MonoBehaviour. Creates the instance on first access if none exists in the scene.
    /// Call SetDontDestroyOnLoad() in OnAwake() to persist across scene loads.
    /// </summary>
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

                _instance = FindAnyObjectByType<T>();

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
            // 破棄後インスタンスに触れないよう、後始末フックを先に呼んでから GameObject を破棄する。
            // DestroyImmediate はランタイムで不安定なため Destroy を使う。
            OnDispose();
            _instance = null;
            Destroy(gameObject);
        }

        protected virtual void OnDispose()
        {
        }
    }
}