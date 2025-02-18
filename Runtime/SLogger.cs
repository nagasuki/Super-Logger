using UnityEngine;

public class SLogger
{
    public static void LogDebug(string message, string category = "General")
    {
        Debug.Log($"[SLogger][{category}] {message}");
    }

    public static void LogWarning(string message, string category = "General")
    {
        Debug.LogWarning($"[SLogger][{category}] {message}");
    }

    public static void LogError(string message, string category = "General")
    {
        Debug.LogError($"[SLogger][{category}] {message}");
    }
}