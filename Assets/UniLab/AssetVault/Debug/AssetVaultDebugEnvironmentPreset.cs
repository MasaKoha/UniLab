using System;
using UnityEngine;

namespace UniLab.AssetVault.Debugging
{
    /// <summary>
    /// QA 用デバッグ環境プリセット 1 件分です。表示名と、適用時に <see cref="AssetVaultRuntime"/> へ
    /// 反映する上書き値（BaseUrl / ContentPath）を保持します。
    /// </summary>
    [Serializable]
    public sealed class AssetVaultDebugEnvironmentPreset
    {
        [SerializeField] private string _displayName;
        [SerializeField] private string _baseUrl;
        [SerializeField] private string _contentPath;

        public AssetVaultDebugEnvironmentPreset(string displayName, string baseUrl, string contentPath)
        {
            _displayName = displayName;
            _baseUrl = baseUrl;
            _contentPath = contentPath;
        }

        /// <summary>ドロップダウンに表示する名前です。</summary>
        public string DisplayName => _displayName;

        /// <summary>上書きする BaseUrl です（例 https://dev.example.com/app）。</summary>
        public string BaseUrl => _baseUrl;

        /// <summary>上書きする ContentPath（版セグメント）です。</summary>
        public string ContentPath => _contentPath;
    }
}
