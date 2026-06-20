using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniLab.AssetVault;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// AssetVault(Addressables) からポップアップ View をロードする IPopupAssetLoader 実装。
    /// View ごとに AssetScope を作って生成し、Release でそのスコープを破棄して Addressables ハンドルを確実に解放する。
    /// 本実装は AssetVault に依存するため、コア（UniLab）とは別アセンブリに置いて依存を隔離する。
    /// </summary>
    public sealed class AssetVaultPopupAssetLoader : IPopupAssetLoader
    {
        private const string AddressPrefix = "Popup/";

        private readonly IAssetVaultService _assetVaultService;

        // View インスタンスと、その生成に使った AssetScope を参照同一性で対応づける。
        // ConditionalWeakTable は Unity の == 上書きに影響されず、破棄済み View でも引ける
        private readonly ConditionalWeakTable<PopupBase, IAssetScope> _scopes = new();

        /// <summary>ロードに用いる AssetVault サービスを注入する。</summary>
        public AssetVaultPopupAssetLoader(IAssetVaultService assetVaultService)
        {
            _assetVaultService = assetVaultService;
        }

        /// <summary>Popup/{型名} アドレスを専用スコープ経由で生成し、非表示で返す。</summary>
        public async UniTask<TPopup> InstantiateAsync<TPopup>(Transform parent, CancellationToken cancellationToken)
            where TPopup : PopupBase
        {
            var scope = _assetVaultService.CreateScope();
            try
            {
                var address = $"{AddressPrefix}{typeof(TPopup).Name}";
                var gameObject = await scope.InstantiateAsync(address, parent, cancellationToken);
                var popup = gameObject.GetComponent<TPopup>();
                if (popup == null)
                {
                    throw new InvalidOperationException(
                        $"生成した {address} に {typeof(TPopup).Name} コンポーネントがありません。");
                }

                popup.gameObject.SetActive(false);
                _scopes.Add(popup, scope);
                return popup;
            }
            catch
            {
                // 生成途中の失敗時もハンドルを取りこぼさないよう、対応スコープを破棄してから送出する
                scope.Dispose();
                throw;
            }
        }

        /// <summary>View に対応するスコープを破棄し、Addressables インスタンスとハンドルを解放する。</summary>
        public void Release(PopupBase popup)
        {
            // 真の null のみ弾く（破棄済み Unity オブジェクトは参照同一性で引けるのでスコープ解放を続行する）
            if (popup is null)
            {
                return;
            }

            if (_scopes.TryGetValue(popup, out var scope))
            {
                _scopes.Remove(popup);
                // scope.Dispose() が Addressables.Release を呼び、生成物の破棄とハンドル解放を行う
                scope.Dispose();
            }
        }
    }
}
