#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// Creates the runtime boot scene and builds the game without opening the
    /// Unity Hub project picker or entering Editor Play Mode.
    /// </summary>
    public static class StandaloneBuild
    {
        private const string SceneFolder = "Assets/Game/Scenes";
        private const string ScenePath = SceneFolder + "/GameStart.unity";
        private const string OutputRelativePath =
            "Builds/Windows/AAA_EconomySLG.exe";
        private const string PreferredBuildRoot = @"D:\AAA_EconomySLG";
        private const string BuildRootEnvironmentVariable =
            "AAA_ECONOMY_SLG_BUILD_ROOT";

        [MenuItem("게임/Windows EXE 빌드")]
        public static void BuildWindowsStandalone()
        {
            EnsureBootScene();

            string absoluteOutputPath = ResolveOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = absoluteOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows EXE 빌드 실패: {summary.result}, 오류 {summary.totalErrors}개");
            }

            Debug.Log(
                $"Windows EXE 빌드 완료: {summary.outputPath} " +
                $"({summary.totalSize / (1024f * 1024f):F1} MB)");
        }

        private static string ResolveOutputPath()
        {
            string buildRoot = Environment.GetEnvironmentVariable(
                BuildRootEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(buildRoot))
            {
                buildRoot = Directory.Exists(@"D:\")
                    ? PreferredBuildRoot
                    : Directory.GetParent(Application.dataPath).FullName;
            }

            return Path.GetFullPath(Path.Combine(
                buildRoot,
                OutputRelativePath));
        }

        private static void EnsureBootScene()
        {
            EnsureFolder(SceneFolder);

            string absoluteScenePath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                ScenePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(absoluteScenePath))
            {
                StarterSceneGenerator.CreateStarterScene();
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            AddToBuildSettings(ScenePath);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
