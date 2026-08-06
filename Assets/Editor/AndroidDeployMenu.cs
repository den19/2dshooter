using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SpacePrisonAsurvil.Editor
{
    /// <summary>
    /// Unity menus to install/deploy the release APK via ADB (USB) or Bluetooth scripts.
    /// </summary>
    public static class AndroidDeployMenu
    {
        const int MenuPriority = 50;
        const string PackageId = "com.densappstudio.asurvil";

        [MenuItem("SpacePrisonAsurvil/Android/Install APK via ADB (USB)", false, MenuPriority)]
        public static void InstallApkViaAdb()
        {
            var apk = AndroidApkBuilder.GetApkOutputPath();
            if (!File.Exists(apk))
            {
                EditorUtility.DisplayDialog(
                    "Install APK via ADB",
                    $"APK not found:\n{apk}\n\nBuild first (SpacePrisonAsurvil → Build Release APK) or use Build and Install.",
                    "OK");
                return;
            }

            var result = RunPowerShellScript("Tools/install_apk_adb.ps1", "-SkipBuild");
            ShowScriptResult("Install APK via ADB", result);
        }

        [MenuItem("SpacePrisonAsurvil/Android/Build and Install APK via ADB (USB)", false, MenuPriority + 1)]
        public static void BuildAndInstallApkViaAdb()
        {
            if (!AndroidApkBuilder.BuildApk(exitEditorOnFinish: false))
            {
                EditorUtility.DisplayDialog(
                    "Build and Install APK via ADB",
                    "Build failed. See the Console.\n\nNeed Tools/keystore.local.ps1 with $KeystorePassword.",
                    "OK");
                return;
            }

            var result = RunPowerShellScript("Tools/install_apk_adb.ps1", "-SkipBuild");
            ShowScriptResult(
                "Build and Install APK via ADB",
                result,
                prefix: $"Built {PlayerSettings.bundleVersion} (code {PlayerSettings.Android.bundleVersionCode}).\n\n");
        }

        [MenuItem("SpacePrisonAsurvil/Android/Deploy APK via Bluetooth", false, MenuPriority + 10)]
        public static void DeployApkViaBluetooth()
        {
            var apk = AndroidApkBuilder.GetApkOutputPath();
            if (!File.Exists(apk))
            {
                EditorUtility.DisplayDialog(
                    "Deploy APK via Bluetooth",
                    $"APK not found:\n{apk}\n\nBuild first or use Build and Deploy via Bluetooth.",
                    "OK");
                return;
            }

            var result = RunPowerShellScript("Tools/deploy_apk_bluetooth.ps1", "-SkipBuild");
            ShowScriptResult("Deploy APK via Bluetooth", result);
        }

        [MenuItem("SpacePrisonAsurvil/Android/Build and Deploy APK via Bluetooth", false, MenuPriority + 11)]
        public static void BuildAndDeployApkViaBluetooth()
        {
            if (!AndroidApkBuilder.BuildApk(exitEditorOnFinish: false))
            {
                EditorUtility.DisplayDialog(
                    "Build and Deploy APK via Bluetooth",
                    "Build failed. See the Console.\n\nNeed Tools/keystore.local.ps1 with $KeystorePassword.",
                    "OK");
                return;
            }

            var result = RunPowerShellScript("Tools/deploy_apk_bluetooth.ps1", "-SkipBuild");
            ShowScriptResult(
                "Build and Deploy APK via Bluetooth",
                result,
                prefix: $"Built {PlayerSettings.bundleVersion} (code {PlayerSettings.Android.bundleVersionCode}).\n\n");
        }

        static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName
                   ?? Directory.GetCurrentDirectory();
        }

        static ScriptRunResult RunPowerShellScript(string relativeScript, string extraArgs)
        {
            var root = GetProjectRoot();
            var scriptPath = Path.Combine(root, relativeScript.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(scriptPath))
            {
                return new ScriptRunResult
                {
                    ExitCode = 1,
                    Output = $"Script not found: {scriptPath}"
                };
            }

            var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {extraArgs}".Trim();
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var output = new StringBuilder();
            try
            {
                using var process = new Process { StartInfo = psi };
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        output.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        output.AppendLine(e.Data);
                };

                EditorUtility.DisplayProgressBar("Android deploy", Path.GetFileName(scriptPath), 0.35f);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                return new ScriptRunResult
                {
                    ExitCode = process.ExitCode,
                    Output = output.ToString().Trim()
                };
            }
            catch (Exception ex)
            {
                return new ScriptRunResult
                {
                    ExitCode = 1,
                    Output = $"Failed to run PowerShell: {ex.Message}\n{output}"
                };
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void ShowScriptResult(string title, ScriptRunResult result, string prefix = "")
        {
            var body = prefix + (string.IsNullOrWhiteSpace(result.Output) ? "(no output)" : result.Output);
            if (body.Length > 1500)
                body = "…\n" + body.Substring(body.Length - 1500);

            if (result.ExitCode == 0)
            {
                Debug.Log($"{title} OK\n{result.Output}");
                EditorUtility.DisplayDialog(title, body, "OK");
            }
            else
            {
                Debug.LogError($"{title} failed (exit {result.ExitCode})\n{result.Output}");
                var hint = result.Output != null &&
                           result.Output.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "\n\nOn the phone: Accept USB debugging."
                    : result.Output != null &&
                      result.Output.IndexOf("INSTALL_FAILED", StringComparison.OrdinalIgnoreCase) >= 0
                        ? $"\n\nIf signature mismatch: adb uninstall {PackageId}"
                        : "";
                EditorUtility.DisplayDialog(title, $"Failed (exit {result.ExitCode}).\n\n{body}{hint}", "OK");
            }
        }

        sealed class ScriptRunResult
        {
            public int ExitCode;
            public string Output;
        }
    }
}
