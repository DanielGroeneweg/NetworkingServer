using UnityEngine;

// This is the class that redirects server log messages to the Unity console.
// Change it to something else (e.g. Console.WriteLine) when needed.
static class Logger
{
    public static void LogInfo(string text)
    {
        Debug.Log("Model: " + text);
    }
}