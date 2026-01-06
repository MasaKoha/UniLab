namespace UniLab.TextManager
{
    public static class KeyHash
    {
        public static uint Fnv1AHash(string input)
        {
            const uint fnvPrime = 16777619;
            var hash = 2166136261;

            foreach (var c in input)
            {
                hash ^= c;
                hash *= fnvPrime;
            }

            return hash;
        }
    }
}