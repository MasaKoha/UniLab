using System.Collections.Generic;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// 同一アドレスへ別アセットが二重登録された場合を収集します。Sync 失敗判定とレポート生成に使います。
    /// Addressables 設定に依存しないため EditMode で単体テストできます。
    /// </summary>
    internal sealed class AssetVaultDuplicateAddressCollector
    {
        private const string DuplicateAddressLineFormat = "重複アドレス: {0}（{1} と {2}）";

        private readonly Dictionary<string, string> _registeredAssetPathsByAddress = new();
        private readonly List<DuplicateAddress> _duplicateAddresses = new();

        /// <summary>重複アドレスが1件以上あるかどうかです。</summary>
        public bool HasDuplicates => _duplicateAddresses.Count > 0;

        /// <summary>アドレスとアセットパスの組を記録します。同一アドレスに別アセットが来たら重複として控えます。</summary>
        public void Record(string address, string assetPath)
        {
            if (!_registeredAssetPathsByAddress.TryGetValue(address, out var registeredAssetPath))
            {
                _registeredAssetPathsByAddress.Add(address, assetPath);
                return;
            }

            if (registeredAssetPath == assetPath)
            {
                return;
            }

            _duplicateAddresses.Add(new DuplicateAddress(address, registeredAssetPath, assetPath));
        }

        /// <summary>重複の一覧を改行区切りのレポート文字列で返します。重複が無ければ空文字です。</summary>
        public string BuildReport()
        {
            if (_duplicateAddresses.Count <= 0)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            foreach (var duplicateAddress in _duplicateAddresses)
            {
                lines.Add(string.Format(
                    DuplicateAddressLineFormat,
                    duplicateAddress.Address,
                    duplicateAddress.FirstAssetPath,
                    duplicateAddress.DuplicateAssetPath));
            }

            return string.Join("\n", lines);
        }

        private readonly struct DuplicateAddress
        {
            public DuplicateAddress(string address, string firstAssetPath, string duplicateAssetPath)
            {
                Address = address;
                FirstAssetPath = firstAssetPath;
                DuplicateAssetPath = duplicateAssetPath;
            }

            public string Address { get; }
            public string FirstAssetPath { get; }
            public string DuplicateAssetPath { get; }
        }
    }
}
