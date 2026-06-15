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

        /// <summary>依存アセット置き場フォルダの名前プレフィックスです。配下は Addressables 登録対象外（依存として同梱）になります。</summary>
        public const string SkipFolderPrefix = "_";

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
        /// assetPath が root 直下または配下にあるかを、フォルダ境界を守って判定します。
        /// </summary>
        public static bool IsUnderRoot(string assetPath, string root)
        {
            var normalizedAssetPath = NormalizeAssetPath(assetPath);
            var normalizedRoot = NormalizeAssetPath(root);
            if (string.IsNullOrEmpty(normalizedAssetPath) || string.IsNullOrEmpty(normalizedRoot))
            {
                return false;
            }

            return normalizedAssetPath == normalizedRoot
                || normalizedAssetPath.StartsWith(normalizedRoot + "/", StringComparison.Ordinal);
        }

        /// <summary>
        /// assetPath が属するカテゴリフォルダを返します。グループ名とラベルを手動 Sync と同じ規則で決めるために使います。
        /// </summary>
        public static string ResolveCategoryFolder(string assetPath, string categoryRoot)
        {
            var normalizedAssetPath = NormalizeAssetPath(assetPath);
            var normalizedCategoryRoot = NormalizeAssetPath(categoryRoot);
            var relativePath = normalizedAssetPath.Substring(normalizedCategoryRoot.Length).TrimStart('/');
            var separatorIndex = relativePath.IndexOf("/", StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                return normalizedCategoryRoot;
            }

            var firstFolderName = relativePath.Substring(0, separatorIndex);
            return normalizedCategoryRoot + "/" + firstFolderName;
        }

        /// <summary>
        /// assetPath が categoryRoot 配下の「_」始まりフォルダ（依存アセット置き場）の中にあるかを判定します。
        /// 単一利用の依存（prefab 専用 AnimationClip、SpriteAtlas の元 Sprite 等）をエントリ登録から除外するために使います。
        /// 判定対象はフォルダ要素のみで、ファイル名先頭の「_」は対象外です。
        /// </summary>
        public static bool IsInSkipFolder(string assetPath, string categoryRoot)
        {
            var normalizedAssetPath = NormalizeAssetPath(assetPath);
            var normalizedCategoryRoot = NormalizeAssetPath(categoryRoot);
            if (!IsUnderRoot(normalizedAssetPath, normalizedCategoryRoot))
            {
                return false;
            }

            var relativePath = normalizedAssetPath.Substring(normalizedCategoryRoot.Length).TrimStart('/');
            var segments = relativePath.Split('/');
            // 末尾要素はファイル名なので除外し、フォルダ要素だけを見る。
            for (var index = 0; index < segments.Length - 1; index++)
            {
                if (segments[index].StartsWith(SkipFolderPrefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
