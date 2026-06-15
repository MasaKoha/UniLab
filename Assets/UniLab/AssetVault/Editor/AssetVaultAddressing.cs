using System;
using System.IO;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// Addressables のアドレス・グループ名・パス正規化を行う純粋ロジックです。
    /// Addressables 設定や AssetDatabase に依存しないため EditMode で単体テストできます。
    /// </summary>
    internal static class AssetVaultAddressing
    {
        /// <summary>Local 配信グループ名のプレフィックスです。</summary>
        public const string LocalGroupPrefix = "Local_";

        /// <summary>Remote 配信グループ名のプレフィックスです。</summary>
        public const string RemoteGroupPrefix = "Remote_";

        /// <summary>
        /// アセットパスから、カテゴリルート相対・拡張子なしのアドレスを作ります。
        /// 例: ("Assets/Remote/Characters/hero.prefab", "Assets/Remote") → "Characters/hero"。
        /// </summary>
        public static string CreateAddress(string assetPath, string categoryRoot)
        {
            var relativePath = assetPath.Substring(categoryRoot.Length + "/".Length);
            var extension = Path.GetExtension(relativePath);
            if (!string.IsNullOrEmpty(extension))
            {
                relativePath = relativePath.Substring(0, relativePath.Length - extension.Length);
            }

            // アドレスは大文字小文字を保持する（アプリ側ロードキーと一致させる）。区切りのみ "/" に統一し前後空白を除去する。
            return relativePath.Replace("\\", "/").Trim();
        }

        /// <summary>
        /// フォルダ名と配信先から Addressables グループ名（Local_&lt;名&gt; / Remote_&lt;名&gt;）を作ります。
        /// </summary>
        public static string GetGroupName(string folderPath, bool isLocal)
        {
            var groupPrefix = isLocal ? LocalGroupPrefix : RemoteGroupPrefix;
            return groupPrefix + Path.GetFileName(folderPath);
        }

        /// <summary>
        /// フォルダ名から一括ロード用ラベル（= フォルダ名そのもの）を作ります。
        /// ラベルは「まとめてロードする単位」を表し、LoadAssetsAsync&lt;T&gt;(label) で横断取得します。
        /// Local/Remote のプレフィックスは付けません（ロードは配信先を区別しない source-agnostic 方針のため）。
        /// </summary>
        public static string CreateLabel(string folderPath)
        {
            return Path.GetFileName(folderPath);
        }

        /// <summary>
        /// アセットパスの区切りを "/" に統一し、末尾スラッシュを除去します。null/空は空文字を返します。
        /// </summary>
        public static string NormalizeAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            return assetPath.Replace("\\", "/").TrimEnd('/');
        }

        /// <summary>
        /// AssetVault が同期で生成・管理するグループ名（Local_/Remote_ プレフィックス）かどうかを序数比較で判定します。
        /// </summary>
        public static bool IsManagedGroupName(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                return false;
            }

            return groupName.StartsWith(LocalGroupPrefix, StringComparison.Ordinal)
                || groupName.StartsWith(RemoteGroupPrefix, StringComparison.Ordinal);
        }
    }
}
