namespace UniLab.Common
{
    public static class UniLabUnique
    {
        private static ulong _id;

        public static ulong GetUniqId()
        {
            _id++;
            return _id;
        }
    }
}