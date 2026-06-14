using System;
using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// 同期対象フォルダ 1 件と、その配信先(Local/Remote)を表すルールです。
    /// 固定のフォルダ規約を置き換え、プロジェクトごとに異なるフォルダ構成へ対応します。
    /// </summary>
    [Serializable]
    public sealed class AssetVaultSyncRule
    {
        [SerializeField] private DefaultAsset _folder;
        [SerializeField] private AssetVaultDeliveryMode _delivery = AssetVaultDeliveryMode.Remote;

        public AssetVaultSyncRule(DefaultAsset folder, AssetVaultDeliveryMode delivery)
        {
            _folder = folder;
            _delivery = delivery;
        }

        /// <summary>配信先(Local=同梱 / Remote=CDN)です。</summary>
        public AssetVaultDeliveryMode Delivery => _delivery;

        /// <summary>Local 配信かどうかです。</summary>
        public bool IsLocal => _delivery == AssetVaultDeliveryMode.Local;

        /// <summary>
        /// 対象フォルダのアセットパスを取得します。未設定・非フォルダの場合は null を返します。
        /// </summary>
        public string ResolveFolderPath()
        {
            if (_folder == null)
            {
                return null;
            }

            // DefaultAsset はフォルダ以外（未知拡張子のファイル等）も代入可能なため、フォルダであることを検証する。
            var folderPath = AssetDatabase.GetAssetPath(_folder);
            return AssetDatabase.IsValidFolder(folderPath) ? folderPath : null;
        }
    }
}
