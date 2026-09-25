using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GasStation.Editor
{
    /// <summary>
    /// Builds the Windows version of the game into Builds/. Also used by CI through
    /// <see cref="BuildWindowsFromCommandLine"/>.
    /// </summary>
    public static class BuildMenu
    {
        private const string DesertScene = "Assets/Scenes/Desert.unity";

        [MenuItem("GasStation/Build/Windows (release)")]
        private static void BuildWindowsRelease() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/GasStation.exe", false);

        [MenuItem("GasStation/Build/Windows (development)")]
        private static void BuildWindowsDevelopment() => Build(BuildTarget.StandaloneWindows64, "Builds/WindowsDev/GasStation.exe", true);

        /// <summary>Entry point for batch mode: -executeMethod GasStation.Editor.BuildMenu.BuildWindowsFromCommandLine</summary>
        public static void BuildWindowsFromCommandLine()
        {
            // Throwing gives batch mode a non-zero exit code; Unity quits by itself afterwards (-quit).
            if (!Build(BuildTarget.StandaloneWindows64, "Builds/Windows/GasStation.exe", false))
                throw new System.Exception("GasStation: Windows build failed, see the log above.");
        }

        private static bool Build(BuildTarget target, string path, bool development)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 && File.Exists(DesertScene))
                scenes = new[] { DesertScene };

            if (scenes.Length == 0)
            {
                Debug.LogError("GasStation: no scenes to build. Build the desert level first (GasStation → Build desert level).");
                return false;
            }

            // Localization tables are Addressables content: build it before the player.
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            if (addressables != null)
            {
                AddressableAssetSettings.BuildPlayerContent(out var result);
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Debug.LogError($"GasStation: Addressables build failed: {result.Error}");
                    return false;
                }
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"GasStation: build {summary.result} with {summary.totalErrors} errors.");
                return false;
            }

            Debug.Log($"GasStation: build succeeded — {path}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss}.");
            if (!Application.isBatchMode)
                EditorUtility.RevealInFinder(path);
            return true;
        }
    }
}
