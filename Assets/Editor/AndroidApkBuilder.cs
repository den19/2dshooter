using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SpacePrisonAsurvil.Editor
{
    /// <summary>
    /// Release Android APK into the project root as <c>SpacePrisonAsurvil.apk</c>.
    /// Bumps Version patch + Bundle Version Code, signs with densappstudio keystore.
    /// Menu: SpacePrisonAsurvil → Build Release APK (project root). Batch: BuildFromBatch.
    /// Invoke only when asked («собери APK»).
    /// </summary>
    public static class AndroidApkBuilder
    {
        public const string ApkFileName = "SpacePrisonAsurvil.apk";
        public const string ReleaseKeystorePath = @"C:/git/MyUnity6.4KeyStore/user.keystore";
        public const string ReleaseKeyAlias = "asurvil";

        const string ScenePath = "Assets/_Scenes/MainMenu.unity";
        const string KeystoreLocalRelative = "Tools/keystore.local.ps1";

        [MenuItem("SpacePrisonAsurvil/Build Release APK (project root)")]
        public static void BuildFromMenu()
        {
            var ok = BuildApk(exitEditorOnFinish: false);
            if (ok)
                EditorUtility.DisplayDialog(
                    "Build Release APK",
                    $"Version {PlayerSettings.bundleVersion} (code {PlayerSettings.Android.bundleVersionCode})\n\nAPK:\n{GetApkOutputPath()}",
                    "OK");
            else
                EditorUtility.DisplayDialog(
                    "Build Release APK",
                    "Build failed. See the Console for details.\n\n" +
                    "Need Tools/keystore.local.ps1 with $KeystorePassword (see .example).",
                    "OK");
        }

        /// <summary>
        /// Headless entry for Tools/build_apk.ps1 (-executeMethod).
        /// </summary>
        public static void BuildFromBatch()
        {
            var ok = BuildApk(exitEditorOnFinish: true);
            if (!ok && !Application.isBatchMode)
                Debug.LogError("Android release APK build failed.");
        }

        public static string GetApkOutputPath()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                              ?? Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(projectRoot, ApkFileName));
        }

        public static bool BuildApk(bool exitEditorOnFinish)
        {
            var outputPath = GetApkOutputPath();
            var exitCode = 0;
            var ok = false;

            try
            {
                if (!ConfigureReleaseSigning())
                {
                    exitCode = 1;
                    return false;
                }

                BumpVersionForRelease();
                EnsureBuildScene();
                EnsureAndroidTarget();
                EditorUserBuildSettings.buildAppBundle = false;

                var scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                    .Select(s => s.path)
                    .ToArray();
                if (scenes.Length == 0)
                {
                    Debug.LogError("AndroidApkBuilder: no enabled scenes in Build Settings.");
                    exitCode = 1;
                    return false;
                }

                Debug.Log(
                    $"AndroidApkBuilder: release build → {outputPath} " +
                    $"(version {PlayerSettings.bundleVersion}, code {PlayerSettings.Android.bundleVersionCode})");

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                ok = summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;

                if (ok)
                {
                    Debug.Log(
                        $"AndroidApkBuilder: succeeded in {summary.totalTime}. " +
                        $"Size≈{summary.totalSize} bytes. Output: {outputPath}");
                    return true;
                }

                Debug.LogError(
                    $"AndroidApkBuilder: failed ({summary.result}). " +
                    $"Errors={summary.totalErrors} Warnings={summary.totalWarnings}");
                exitCode = 1;
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"AndroidApkBuilder: exception — {ex}");
                exitCode = 1;
                ok = false;
                return false;
            }
            finally
            {
                if (exitEditorOnFinish && Application.isBatchMode)
                    EditorApplication.Exit(ok ? 0 : exitCode);
            }
        }

        /// <summary>
        /// Increments the last numeric segment after the last dot (1.2.10 → 1.2.11)
        /// and sets Android bundleVersionCode to that integer.
        /// </summary>
        public static void BumpVersionForRelease()
        {
            var current = PlayerSettings.bundleVersion ?? "1.0.0";
            var previousCode = Math.Max(0, PlayerSettings.Android.bundleVersionCode);
            var bumped = IncrementLastVersionSegment(current, out var code);
            if (code <= previousCode)
                code = previousCode + 1;

            PlayerSettings.bundleVersion = bumped;
            PlayerSettings.Android.bundleVersionCode = code;
            AssetDatabase.SaveAssets();
            Debug.Log($"AndroidApkBuilder: version {current} → {bumped}, bundleVersionCode={code}");
        }

        public static string IncrementLastVersionSegment(string version, out int lastSegment)
        {
            lastSegment = 1;
            if (string.IsNullOrWhiteSpace(version))
            {
                lastSegment = 1;
                return "1.0.1";
            }

            var trimmed = version.Trim();
            var dot = trimmed.LastIndexOf('.');
            string prefix;
            string last;
            if (dot < 0)
            {
                prefix = "";
                last = trimmed;
            }
            else
            {
                prefix = trimmed.Substring(0, dot + 1);
                last = trimmed.Substring(dot + 1);
            }

            if (!int.TryParse(last, out var n) || n < 0)
            {
                lastSegment = 1;
                return string.IsNullOrEmpty(prefix) ? "1" : prefix + "1";
            }

            lastSegment = n + 1;
            return prefix + lastSegment.ToString();
        }

        static bool ConfigureReleaseSigning()
        {
            var password = ResolveKeystorePassword();
            if (string.IsNullOrEmpty(password))
            {
                Debug.LogError(
                    "AndroidApkBuilder: release signing password missing. " +
                    "Create Tools/keystore.local.ps1 from Tools/keystore.local.ps1.example " +
                    "(set $KeystorePassword), or set ANDROID_KEYSTORE_PASS. " +
                    "Debug signing is disabled.");
                return false;
            }

            if (!File.Exists(ReleaseKeystorePath))
            {
                Debug.LogError(
                    $"AndroidApkBuilder: keystore not found at {ReleaseKeystorePath}");
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ReleaseKeystorePath;
            PlayerSettings.Android.keyaliasName = ReleaseKeyAlias;
            PlayerSettings.Android.keystorePass = password;
            PlayerSettings.Android.keyaliasPass = password;
            return true;
        }

        static string ResolveKeystorePassword()
        {
            var fromEnv = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
            if (!string.IsNullOrEmpty(fromEnv))
                return fromEnv;

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return null;

            var localPath = Path.Combine(projectRoot, KeystoreLocalRelative);
            if (!File.Exists(localPath))
                return null;

            var text = File.ReadAllText(localPath);
            var match = Regex.Match(
                text,
                @"\$KeystorePassword\s*=\s*(?:'([^']*)'|""([^""]*)"")",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return null;

            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return string.IsNullOrWhiteSpace(value) || value == "YOUR_PASSWORD" ? null : value;
        }

        /// <summary>
        /// Ensures MainMenu is enabled in Build Settings without wiping other scenes.
        /// </summary>
        static void EnsureBuildScene()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes != null &&
                scenes.Any(s => s.enabled && s.path == ScenePath))
                return;

            var list = scenes != null
                ? new List<EditorBuildSettingsScene>(scenes)
                : new List<EditorBuildSettingsScene>();

            var existing = list.FirstOrDefault(s => s.path == ScenePath);
            if (existing != null)
            {
                existing.enabled = true;
            }
            else
            {
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            }

            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"AndroidApkBuilder: ensured {ScenePath} is enabled in Build Settings.");
        }

        static void EnsureAndroidTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                return;

            var switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android,
                BuildTarget.Android);
            if (!switched)
                throw new InvalidOperationException(
                    "Failed to switch active build target to Android. " +
                    "Install Android Build Support in Unity Hub.");
        }
    }
}
