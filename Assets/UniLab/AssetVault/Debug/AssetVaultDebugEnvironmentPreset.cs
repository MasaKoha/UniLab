using System;
using UnityEngine;

namespace UniLab.AssetVault.Debugging
{
    /// <summary>
    /// QA 用デバッグ環境プリセット 1 件分です。表示名と、適用時に <see cref="AssetVaultRuntime"/> の
    /// BaseUrl へ反映する上書き値を保持します。ContentPath（版）は上書きせず version.json 解決に任せます。
    /// </summary>
    [Serializable]
    public sealed class AssetVaultDebugEnvironmentPreset
    {
        [SerializeField] private string _displayName;
        [SerializeField] private string _baseUrl;

        public AssetVaultDebugEnvironmentPreset(string displayName, string baseUrl)
        {
            _displayName = displayName;
            _baseUrl = baseUrl;
        }

        /// <summary>ドロップダウンに表示する名前です。</summary>
        public string DisplayName => _displayName;

        /// <summary>上書きする BaseUrl です（例 https://dev.example.com/app）。</summary>
        public string BaseUrl => _baseUrl;
    }
}
