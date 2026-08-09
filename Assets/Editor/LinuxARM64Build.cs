using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public class LinuxARM64Build : MonoBehaviour
{
    [MenuItem("Build/Linux ARM64")]
    public static void BuildLinuxARM64()
    {
        string outputPath = Path.Combine(Application.dataPath.Replace("Assets", ""), "builds", "yarg-arm64", "YARG");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        var buildTarget = BuildTarget.StandaloneLinux64;
        var options = BuildOptions.None;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes,
            target = buildTarget,
            locationPathName = outputPath,
            options = options
        });

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {outputPath} ({report.summary.totalSize / (1024f * 1024f):F1} MB)");
        }
        else
        {
            Debug.LogError($"Build failed: {report.summary.result}");
        }
    }
}
