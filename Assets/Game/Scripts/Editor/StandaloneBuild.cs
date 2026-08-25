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
            "Builds/Windows/SyndicatesAndEmpires.exe";
        private const string WebGlOutputFolder = "docs";
        private const string WebGlStagingFolder = "Temp/SyndicatesAndEmpires";
        private const string WebGlTemplate = "PROJECT:SyndicatesAndEmpires";
        private const string PreferredBuildRoot = @"D:\SyndicatesAndEmpires";
        private const string BuildRootEnvironmentVariable =
            "SYNDICATES_AND_EMPIRES_BUILD_ROOT";

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

        [MenuItem("게임/SYNDICATES & EMPIRES WebGL 제출 빌드")]
        public static void BuildWebGlSubmission()
        {
            EnsureBootScene();

            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.WebGL,
                    BuildTarget.WebGL))
            {
                throw new InvalidOperationException(
                    "WebGL Build Support가 설치되어 있지 않습니다.");
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.WebGL,
                BuildTarget.WebGL);

            // GitHub Pages does not provide Unity-specific Content-Encoding
            // headers. Uncompressed output works on any static host without
            // server configuration or a JavaScript decompression fallback.
            PlayerSettings.WebGL.compressionFormat =
                WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = WebGlTemplate;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string stagingPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                WebGlStagingFolder));
            string outputPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                WebGlOutputFolder));

            if (Directory.Exists(stagingPath))
            {
                Directory.Delete(stagingPath, true);
            }

            Directory.CreateDirectory(stagingPath);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = stagingPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"SYNDICATES & EMPIRES WebGL 빌드 실패: {summary.result}, " +
                    $"오류 {summary.totalErrors}개");
            }

            ApplyWebGlVersionedFilenames(stagingPath);
            File.WriteAllText(Path.Combine(stagingPath, ".nojekyll"), string.Empty);
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            Directory.Move(stagingPath, outputPath);
            Debug.Log(
                $"SYNDICATES & EMPIRES WebGL 빌드 완료: {outputPath} " +
                $"({summary.totalSize / (1024f * 1024f):F1} MB)");
        }

        private static void ApplyWebGlVersionedFilenames(string stagingPath)
        {
            const string placeholder = "__BUILD_VERSION__";
            string indexPath = Path.Combine(stagingPath, "index.html");
            string html = File.ReadAllText(indexPath);
            if (!html.Contains(placeholder))
            {
                throw new InvalidOperationException(
                    "WebGL 템플릿에 캐시 버전 자리표시자가 없습니다.");
            }

            string buildVersion = DateTime.UtcNow.ToString(
                "yyyyMMddHHmmss");
            string buildFolder = Path.Combine(stagingPath, "Build");
            string[] buildFiles = Directory.GetFiles(buildFolder);
            int replacedReferenceCount = 0;
            for (int i = 0; i < buildFiles.Length; i++)
            {
                string sourcePath = buildFiles[i];
                string sourceName = Path.GetFileName(sourcePath);
                int extensionIndex = sourceName.IndexOf('.');
                if (extensionIndex <= 0)
                    continue;

                string versionedName = sourceName.Insert(
                    extensionIndex,
                    "." + buildVersion);
                string sourceUrl =
                    "Build/" + sourceName + "?v=" + placeholder;
                if (html.Contains(sourceUrl))
                {
                    html = html.Replace(
                        sourceUrl,
                        "Build/" + versionedName);
                    replacedReferenceCount++;
                }

                File.Move(
                    sourcePath,
                    Path.Combine(buildFolder, versionedName));
            }

            if (replacedReferenceCount < 4 || html.Contains(placeholder))
            {
                throw new InvalidOperationException(
                    "WebGL 빌드 파일을 고유 버전 이름으로 연결하지 못했습니다.");
            }

            File.WriteAllText(
                indexPath,
                html);
            File.WriteAllText(
                Path.Combine(stagingPath, "build-version.txt"),
                buildVersion);
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
