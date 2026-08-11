using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Editor
{
    public static class HiveConnectionSetup
    {
        private const string DefineSymbol = "HIVE_MATCHMAKING_ENABLED";
        private const string HiveSdkScriptFolder =
            "Assets/Hive_SDK_v4/Script";

        [MenuItem("게임/HIVE 연결/HIVE 매칭 활성화")]
        private static void EnableHiveMatchmaking()
        {
            if (!AssetDatabase.IsValidFolder(HiveSdkScriptFolder))
            {
                EditorUtility.DisplayDialog(
                    "HIVE SDK 필요",
                    "HIVE Unity Interface와 Windows 패키지를 먼저 " +
                    "가져오세요. 자세한 순서는 HIVE_CONNECTION_KO.md에 " +
                    "정리되어 있습니다.",
                    "확인");
                return;
            }

            SetStandaloneDefine(enabled: true);
            Debug.Log(
                "HIVE 매칭 연결 코드를 활성화했습니다. Unity가 스크립트를 " +
                "다시 컴파일합니다.");
        }

        [MenuItem("게임/HIVE 연결/HIVE 매칭 비활성화")]
        private static void DisableHiveMatchmaking()
        {
            SetStandaloneDefine(enabled: false);
            Debug.Log("HIVE 매칭 연결 코드를 비활성화했습니다.");
        }

        [MenuItem("게임/HIVE 연결/HIVE SDK 설치 상태 확인")]
        private static void ShowHiveSdkStatus()
        {
            bool installed = AssetDatabase.IsValidFolder(HiveSdkScriptFolder);
            bool enabled = GetStandaloneDefines().Contains(DefineSymbol);
            EditorUtility.DisplayDialog(
                "HIVE 연결 상태",
                $"Unity Interface: {(installed ? "설치됨" : "미설치")}\n" +
                $"연결 코드: {(enabled ? "활성" : "비활성")}\n\n" +
                "실제 매칭에는 HIVE AppID, 보안 키, 로그인 세션, " +
                "콘솔 매치 ID가 필요합니다.",
                "확인");
        }

        private static void SetStandaloneDefine(bool enabled)
        {
            HashSet<string> definitions = GetStandaloneDefines();
            if (enabled)
                definitions.Add(DefineSymbol);
            else
                definitions.Remove(DefineSymbol);

            var ordered = new List<string>(definitions);
            ordered.Sort(StringComparer.Ordinal);
            PlayerSettings.SetScriptingDefineSymbols(
                NamedBuildTarget.Standalone,
                string.Join(";", ordered));
        }

        private static HashSet<string> GetStandaloneDefines()
        {
            string raw = PlayerSettings.GetScriptingDefineSymbols(
                NamedBuildTarget.Standalone);
            return new HashSet<string>(
                raw.Split(
                    new[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
        }
    }
}
