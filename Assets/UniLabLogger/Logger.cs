using UnityEngine;

public static class Debug
{
#if DEBUG
    public static void Log(object msg)
    {
        UnityEngine.Debug.Log(msg);
    }

    public static void LogWarning(object msg)
    {
        UnityEngine.Debug.LogWarning(msg);
    }

    public static void LogError(object msg)
    {
        UnityEngine.Debug.LogError(msg);
    }

    public static void LogWarning(string format, GameObject gameObject)
    {
        UnityEngine.Debug.LogWarningFormat(gameObject, format);
    }
#else
    public static void Log(object msg)
    {
    }

    public static void LogWarning(object msg)
    {
    }

    public static void LogError(object msg)
    {
    }

    public static void LogWarning(string format, GameObject gameObject)
    {
    }
#endif
}