#if UNITY_EDITOR
using System;
using UnityEditor;

namespace Game.Editor
{
    public static class SinglePlayerEditorLauncher
    {
        private const string PendingKey =
            "Game.Editor.SinglePlayerEditorLauncher.Pending";
        private const string AutoLaunchConsumedKey =
            "Game.Editor.SinglePlayerEditorLauncher.AutoLaunchConsumed";
        private const string AutoLaunchArgument = "-economyLaunchGame";

        [MenuItem("게임/게임 실행")]
        public static void Launch()
        {
            SessionState.SetBool(PendingKey, true);
            EditorApplication.update -= EnterPlayModeWhenReady;
            EditorApplication.update += EnterPlayModeWhenReady;
        }

        // Also resumes automatically after package installation or script reload.
        [InitializeOnLoadMethod]
        private static void ResumeAfterScriptReload()
        {
            if (HasCommandLineArgument(AutoLaunchArgument) &&
                !SessionState.GetBool(AutoLaunchConsumedKey, false))
            {
                SessionState.SetBool(AutoLaunchConsumedKey, true);
                SessionState.SetBool(PendingKey, true);
            }

            if (!SessionState.GetBool(PendingKey, false))
                return;

            EditorApplication.update -= EnterPlayModeWhenReady;
            EditorApplication.update += EnterPlayModeWhenReady;
        }

        private static bool HasCommandLineArgument(string expected)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, expected,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnterPlayModeWhenReady()
        {
            if (!SessionState.GetBool(PendingKey, false))
            {
                EditorApplication.update -= EnterPlayModeWhenReady;
                return;
            }
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            SessionState.EraseBool(PendingKey);
            EditorApplication.update -= EnterPlayModeWhenReady;
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
