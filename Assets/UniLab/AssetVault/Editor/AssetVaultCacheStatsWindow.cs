using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// Play 中の <see cref="IAssetVaultCache"/> の占有状況（<see cref="AssetVaultCacheStats"/>）を Editor 上で可視化するウィンドウです。
    /// 実行中の cache は <see cref="AssetVaultCacheStatsRegistry"/> 経由で取得します。
    /// </summary>
    public sealed class AssetVaultCacheStatsWindow : EditorWindow
    {
        private const string WindowMenuPath = "UniLab/AssetVault/Cache Stats";
        private const string WindowTitle = "AssetVault Cache Stats";
        private const float LabelWidth = 200f;

        [MenuItem(WindowMenuPath)]
        private static void Open()
        {
            var window = GetWindow<AssetVaultCacheStatsWindow>();
            window.titleContent = new(WindowTitle);
            window.Show();
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode and ensure a cache is constructed.", MessageType.Info);
                return;
            }

            if (!AssetVaultCacheStatsRegistry.TryGetStats(out var stats))
            {
                EditorGUILayout.HelpBox("No AssetVaultCache is registered. Construct a cache to see stats.", MessageType.Warning);
                return;
            }

            using (new LabelWidthScope(LabelWidth))
            {
                EditorGUILayout.LabelField("Entry Count", stats.EntryCount.ToString());
                EditorGUILayout.LabelField("Referenced Entry Count", stats.ReferencedEntryCount.ToString());
                EditorGUILayout.LabelField("Pinned Entry Count", stats.PinnedEntryCount.ToString());
                EditorGUILayout.LabelField("Unreferenced Entry Count", stats.UnreferencedEntryCount.ToString());
                EditorGUILayout.LabelField("Total Reference Count", stats.TotalReferenceCount.ToString());

                if (AssetVaultCacheStatsRegistry.TryGetEstimatedMemoryBytes(out var estimatedBytes))
                {
                    EditorGUILayout.LabelField("Estimated Memory", FormatBytes(estimatedBytes));
                }
            }

            EditorGUILayout.Space();
            // 未参照（TTL 猶予中・LRU 超過）エントリを即時解放して、解放挙動を手元で確認できるようにする。
            if (GUILayout.Button("Trim (release expired / over-capacity)"))
            {
                AssetVaultCacheStatsRegistry.TryTrim();
            }
        }

        /// <summary>
        /// バイト数を B/KB/MB の読みやすい表記へ変換します（ASCII 限定）。
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            const long kiloByte = 1024L;
            const long megaByte = kiloByte * 1024L;
            if (bytes >= megaByte)
            {
                return $"{bytes / (float)megaByte:F2} MB";
            }

            if (bytes >= kiloByte)
            {
                return $"{bytes / (float)kiloByte:F2} KB";
            }

            return $"{bytes} B";
        }

        private void Update()
        {
            // Play 中は値が変化するため再描画する。GUI 操作が無いと OnGUI は呼ばれないため明示的に Repaint する。
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        /// <summary>
        /// IMGUI の EditorGUIUtility.labelWidth を一時的に変更し、Dispose で元に戻すスコープです。
        /// </summary>
        private readonly struct LabelWidthScope : System.IDisposable
        {
            private readonly float _previousLabelWidth;

            public LabelWidthScope(float labelWidth)
            {
                _previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = labelWidth;
            }

            public void Dispose()
            {
                EditorGUIUtility.labelWidth = _previousLabelWidth;
            }
        }
    }
}
