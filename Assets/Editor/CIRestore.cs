#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

[InitializeOnLoad]
public class CIRestore
{
    static CIRestore()
    {
        if (!ShouldRestore())
            return;

        Debug.Log("CIRestore: Starting NuGet package restoration...");
        Stopwatch sw = Stopwatch.StartNew();

        // Force Unity to resolve all packages including YARG.Core
        try
        {
            var request = Client.Resolve();
            while (!request.IsCompleted)
            {
                System.Threading.Thread.Sleep(100);
            }

            if (request.Status == StatusCode.Success)
            {
                Debug.Log($"CIRestore: Package resolution completed in {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                Debug.LogError($"CIRestore: Package resolution failed: {request.Error?.message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CIRestore: Exception during package resolution: {e.Message}");
        }

        // Mark restoration as done
        string markerPath = Path.Combine(Application.dataPath, "..", "ci_restore_done.txt");
        try
        {
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log("CIRestore: Done marker written.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"CIRestore: Could not write marker: {e.Message}");
        }
    }

    static bool ShouldRestore()
    {
        if (!EditorApplication.isBatchMode)
            return false;

        string markerPath = Path.Combine(Application.dataPath, "..", "ci_restore_done.txt");
        if (File.Exists(markerPath))
            return false;

        // Check if we're in a CI environment
        string env = System.Environment.GetEnvironmentVariable("CI");
        return env != null && env.ToLower() == "true";
    }
}
#endif
