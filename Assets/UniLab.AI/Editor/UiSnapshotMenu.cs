using UnityEditor;
using UnityEngine;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// UI スナップショットと入力オーバーレイの手動確認入口です。
    /// 設計どおりの呼び出し口を Play 中に即検証できるようにします。
    /// </summary>
    public static class UiSnapshotMenu
    {
        private const string CaptureMenuPath = "UniLab/Debug/Capture UI Snapshot";
        private const string LogCompactTextMenuPath = "UniLab/Debug/Log UI Snapshot Compact Text";
        private const string ShowOverlayMenuPath = "UniLab/Debug/Show Input Overlay";
        private const string HideOverlayMenuPath = "UniLab/Debug/Hide Input Overlay";

        /// <summary>
        /// 現在の UI 状態を JSON 保存します。
        /// 画像を開かず機械処理へ回せる成果物を即時に残すためです。
        /// </summary>
        [MenuItem(CaptureMenuPath)]
        private static void CaptureUiSnapshot()
        {
            var snapshot = UiSnapshot.Capture();
            var outputFilePath = UiSnapshot.Save(snapshot);
            var elementCount = snapshot.elements == null ? 0 : snapshot.elements.Length;
            UnityEngine.Debug.Log($"UI スナップショットを保存しました。 elements={elementCount}, path={outputFilePath}");
        }

        /// <summary>
        /// 現在の UI 状態を圧縮テキストでログへ出します。
        /// `execute_code` と同じ読ませ方を Editor だけで再現できるようにします。
        /// </summary>
        [MenuItem(LogCompactTextMenuPath)]
        private static void LogUiSnapshotCompactText()
        {
            var snapshot = UiSnapshot.Capture();
            UnityEngine.Debug.Log(UiSnapshot.ToCompactText(snapshot));
        }

        /// <summary>
        /// 入力オーバーレイを Play 中に手動表示します。
        /// 録画前に配置や見え方を単体確認できるようにするためです。
        /// </summary>
        [MenuItem(ShowOverlayMenuPath)]
        private static void ShowInputOverlay()
        {
            if (!Application.isPlaying)
            {
                UnityEngine.Debug.LogError("[InputOverlay] Play 中のみ表示できます。");
                return;
            }

            InputOverlay.Show();
        }

        /// <summary>
        /// 入力オーバーレイを手動で消します。
        /// 録画外の目視確認後に即座に画面を元へ戻せるようにします。
        /// </summary>
        [MenuItem(HideOverlayMenuPath)]
        private static void HideInputOverlay()
        {
            if (!Application.isPlaying)
            {
                UnityEngine.Debug.LogError("[InputOverlay] Play 中のみ非表示にできます。");
                return;
            }

            InputOverlay.Hide();
        }
    }
}
