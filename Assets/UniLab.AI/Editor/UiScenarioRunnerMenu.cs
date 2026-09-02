using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// UI シナリオを選択して Play 中に自動実行するエディタメニューです。
    /// </summary>
    [InitializeOnLoad]
    public static class UiScenarioRunnerMenu
    {
        private const string MenuPath = "UniLab/Debug/Run UI Scenario...";
        private const string SessionStatePathKey = "UniLab.AI.UiScenarioRunnerMenu.ScenarioPath";

        static UiScenarioRunnerMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// シナリオ JSON を選択して先頭 BuildSettings シーンから Play を開始します。
        /// </summary>
        [MenuItem(MenuPath)]
        private static void RunUiScenario()
        {
            if (EditorApplication.isPlaying)
            {
                UnityEngine.Debug.LogError("[UiScenarioRunner] Play 中は開始できません。");
                return;
            }

            var selectedScenarioPath = EditorUtility.OpenFilePanel("UI シナリオを選択", Application.dataPath, "json");
            if (string.IsNullOrEmpty(selectedScenarioPath))
            {
                return;
            }

            RunScenarioFile(selectedScenarioPath);
        }

        /// <summary>
        /// パス指定でシナリオを開始する。ファイル選択ダイアログを出せない外部自動化
        /// （MCP ブリッジ等）から利用側プロジェクトの固定メニューが呼ぶ入口。
        /// </summary>
        public static void RunScenarioFile(string scenarioPath)
        {
            if (EditorApplication.isPlaying)
            {
                UnityEngine.Debug.LogError("[UiScenarioRunner] Play 中は開始できません。");
                return;
            }

            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes == null || buildScenes.Length == 0)
            {
                UnityEngine.Debug.LogError("[UiScenarioRunner] EditorBuildSettings にシーンがありません。");
                return;
            }

            SessionState.SetString(SessionStatePathKey, scenarioPath);
            EditorSceneManager.OpenScene(buildScenes[0].path, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange playModeState)
        {
            if (playModeState != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            var scenarioPath = SessionState.GetString(SessionStatePathKey, string.Empty);
            if (string.IsNullOrEmpty(scenarioPath))
            {
                return;
            }

            SessionState.SetString(SessionStatePathKey, string.Empty);
            if (!File.Exists(scenarioPath))
            {
                UnityEngine.Debug.LogError($"[UiScenarioRunner] シナリオファイルが見つかりません。 path={scenarioPath}");
                EditorApplication.ExitPlaymode();
                return;
            }

            var scenarioJson = File.ReadAllText(scenarioPath);
            var scenario = JsonUtility.FromJson<UiScenario>(scenarioJson);
            if (scenario == null)
            {
                UnityEngine.Debug.LogError($"[UiScenarioRunner] シナリオ JSON の読み込みに失敗しました。 path={scenarioPath}");
                EditorApplication.ExitPlaymode();
                return;
            }

            var runner = UiScenarioRunner.Run(scenario);
            runner.Completed += HandleRunnerCompleted;
        }

        private static void HandleRunnerCompleted()
        {
            EditorApplication.ExitPlaymode();
        }
    }
}
