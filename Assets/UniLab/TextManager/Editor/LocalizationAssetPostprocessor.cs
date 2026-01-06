using UnityEditor;
using UnityEngine;

namespace UniLab.TextManager.Editor
{
#if UNITY_EDITOR
    public class LocalizationAssetPostprocessor : AssetPostprocessor
    {
        // 3秒以内の重複を防ぐ
        private const double MinInterval = 3;
        private static double _lastProcessedTime = 0;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastProcessedTime < MinInterval)
            {
                return;
            }

            foreach (var assetPath in importedAssets)
            {
                if (!assetPath.EndsWith("LocalizationData.asset"))
                {
                    continue;
                }

                TextManager.ResetLoadedAsset();
                LocalizedTextUpdater.UpdateAllLocalizedTexts();
                _lastProcessedTime = now;
                Debug.Log("LocalizationData.asset インポート後に LocalizedText を更新しました。");
            }
        }
    }
#endif
}