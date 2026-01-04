using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class DevLog
{
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Info(string category, string message)
    {
        Debug.Log($"[{category}] {message}");
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Warn(string category, string message)
    {
        Debug.LogWarning($"[{category}] {message}");
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Error(string category, string message)
    {
        Debug.LogError($"[{category}] {message}");
    }
}