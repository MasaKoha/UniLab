namespace UniLab.Common
{
    public abstract class Singleton<T> where T : class, new()
    {
        private static T _instance;
        public static T Instance => _instance ??= new T();

        public static T Initialize(T data)
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = data;
            return _instance;
        }
    }
}