using UnityEngine;

public class SLogger
{
    public static void LogDebug(string message, string category = "General")
    {
        Debug.Log($"[{category}] {message}");
    }

    public static void LogWarning(string message, string category = "General")
    {
        Debug.LogWarning($"[{category}] {message}");
    }

    public static void LogError(string message, string category = "General")
    {
        Debug.LogError($"[{category}] {message}");
    }
}