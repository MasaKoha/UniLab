using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.AssetVault;

namespace UniLab.Localization
{
    /// <summary>
    /// AssetVault（Addressables）から翻訳データを読む ILocalizationDataSource。
    ///
    /// ILocalizationDataSource.Load が同期のため、先に LoadAsync で取り込んでから使う。
    /// Resources へ置いたアセットはビルドから剥がせず起動時のインデックスにも載るので、
    /// 配信で読める経路をこちらに用意する。
    /// </summary>
    public sealed class AssetVaultLocalizationDataSource : ILocalizationDataSource
    {
        /// <summary>翻訳データの既定のアドレス。</summary>
        public const string DefaultAddress = "Localization/LocalizationData";

        private readonly IAssetScope _assetScope;
        private readonly string _address;
        private LocalizationData _loaded;

        /// <summary>読み込みに使う AssetScope と、翻訳データのアドレスを受け取る。</summary>
        public AssetVaultLocalizationDataSource(IAssetScope assetScope, string address = DefaultAddress)
        {
            _assetScope = assetScope ?? throw new ArgumentNullException(nameof(assetScope));
            _address = string.IsNullOrWhiteSpace(address) ? DefaultAddress : address.Trim();
        }

        /// <summary>翻訳データを取り込む。TextManager へ渡す前に一度だけ呼ぶ。</summary>
        public async UniTask LoadAsync(CancellationToken cancellationToken)
        {
            _loaded = await _assetScope.LoadAssetAsync<LocalizationData>(_address, cancellationToken);
            if (_loaded == null)
            {
                throw new InvalidOperationException(
                    $"翻訳データを取得できませんでした。アドレス: {_address}。Addressables への登録を確認すること。");
            }
        }

        /// <summary>取り込み済みの翻訳データを返す。LoadAsync より前に呼ぶと例外。</summary>
        public LocalizationData Load()
        {
            if (_loaded == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(LoadAsync)} より前に {nameof(Load)} が呼ばれました。読み込み完了後に TextManager へ渡すこと。");
            }

            return _loaded;
        }
    }
}
