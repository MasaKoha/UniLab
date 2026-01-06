using System;
using System.Collections.Generic;

namespace UniLab.TextManager
{
    [Serializable]
    public class LocalizationEntry
    {
        public string Key;
        public uint Hash;
        public List<string> Values;
    }
}