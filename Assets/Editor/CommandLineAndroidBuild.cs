using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Invoked via batchmode: -executeMethod CommandLineAndroidBuild.PerformBuild</summary>
public static class CommandLineAndroidBuild
{
    const string DefaultRelativeApk = "Builds/10052026/2dshooter.apk";

    public static void PerformBuild()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (projectRoot == null)
        {
            Debug.LogError("Could not resolve project root.");
            EditorApplication.Exit(1);
            return;
        }

        string relative = ResolveArg("-androidApkRelative", DefaultRelativeApk);
        string apkPath = Path.IsPathRooted(relative)
            ? relative
            : Path.Combine(projectRoot, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(apkPath) ?? "");

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Android build failed: {report.summary.result} ({report.summary.totalErrors} errors).");
            EditorApplication.Exit(1);
        }
    }

    static string ResolveArg(string flag, string defaultValue)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag)
                return args[i + 1];
        }
        return defaultValue;
    }
}
