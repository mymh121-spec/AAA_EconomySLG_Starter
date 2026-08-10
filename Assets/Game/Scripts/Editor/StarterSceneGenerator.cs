#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Presentation;

namespace Game.Editor
{
    public static class StarterSceneGenerator
    {
        private const string SceneFolder = "Assets/Game/Scenes";
        private const string ScenePath = SceneFolder + "/GameStart.unity";

        [MenuItem("게임/기본 경제 맵 씬 생성")]
        public static void CreateStarterScene()
        {
            EnsureFolder(SceneFolder);
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            var modeRoot = new GameObject("게임 모드 선택");
            modeRoot.AddComponent<GameModeSelectionController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Selection.activeGameObject = modeRoot;
            Debug.Log("게임 모드 선택 시작 씬을 생성했습니다: " + ScenePath);
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                    return;
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
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
