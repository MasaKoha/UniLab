using System;

namespace UniLab.Persistence
{
    /// <summary>
    /// Marks a class or struct as a source of PlayerPrefs key constants for UniLab editor tooling.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class UniLabPlayerPrefsKeySourceAttribute : Attribute
    {
    }
}
