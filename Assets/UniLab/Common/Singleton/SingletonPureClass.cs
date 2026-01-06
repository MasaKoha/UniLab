using System;

namespace UniLab.Common
{
    public abstract class SingletonPureClass<T> : IDisposable where T : SingletonPureClass<T>, new()
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                _instance ??= new T();
                return _instance;
            }
        }

        public void Dispose()
        {
            OnDispose();
            _instance = null;
        }

        protected virtual void OnDispose()
        {
        }
    }
}