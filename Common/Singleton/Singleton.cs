namespace UniLab.Common
{
    public abstract class Singleton<TData> where TData : class
    {
        private static TData _instance;

        public static TData Instance => _instance;

        public static TData Initialize(TData data)
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