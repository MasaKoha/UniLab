using System;
using UnityEngine;

namespace UniLab.Persistence
{
    /// <summary>
    /// Type-safe wrapper around PlayerPrefs that handles bool, int, float, string, and Enum values.
    /// Enum values are persisted as their underlying int representation.
    /// </summary>
    public static class PlayerPrefsWrapper
    {
        /// <summary>
        /// Saves a value of type T to PlayerPrefs.
        /// Supported types: bool, int, float, string, Enum.
        /// </summary>
        public static void Set<T>(string key, T value)
        {
            switch (value)
            {
                case bool boolValue:
                    PlayerPrefs.SetInt(key, boolValue ? 1 : 0);
                    break;
                case int intValue:
                    PlayerPrefs.SetInt(key, intValue);
                    break;
                case float floatValue:
                    PlayerPrefs.SetFloat(key, floatValue);
                    break;
                case string stringValue:
                    PlayerPrefs.SetString(key, stringValue);
                    break;
                default:
                    // Enum types are not matched by pattern matching on their base type,
                    // so check explicitly after the switch.
                    if (typeof(T).IsEnum)
                    {
                        PlayerPrefs.SetInt(key, Convert.ToInt32(value));
                        break;
                    }
                    throw new NotSupportedException($"PlayerPrefsWrapper does not support type {typeof(T).FullName}.");
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Loads a value of type T from PlayerPrefs, returning defaultValue if the key is absent.
        /// Supported types: bool, int, float, string, Enum.
        /// </summary>
        public static T Get<T>(string key, T defaultValue = default)
        {
            if (typeof(T) == typeof(bool))
            {
                var intValue = PlayerPrefs.GetInt(key, Convert.ToInt32(defaultValue));
                return (T)(object)(intValue != 0);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)PlayerPrefs.GetInt(key, Convert.ToInt32(defaultValue));
            }

            if (typeof(T) == typeof(float))
            {
                var floatDefault = defaultValue is float f ? f : 0f;
                return (T)(object)PlayerPrefs.GetFloat(key, floatDefault);
            }

            if (typeof(T) == typeof(string))
            {
                var stringDefault = defaultValue as string ?? string.Empty;
                return (T)(object)PlayerPrefs.GetString(key, stringDefault);
            }

            if (typeof(T).IsEnum)
            {
                var intDefault = Convert.ToInt32(defaultValue);
                var intValue = PlayerPrefs.GetInt(key, intDefault);
                return (T)Enum.ToObject(typeof(T), intValue);
            }

            throw new NotSupportedException($"PlayerPrefsWrapper does not support type {typeof(T).FullName}.");
        }
    }
}
