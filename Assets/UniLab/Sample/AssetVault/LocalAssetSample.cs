using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.AssetVault.Sample
{
    /// <summary>
    /// Local（プレイヤー同梱）アセットの読み込みサンプルです。
    /// ロード API は Remote と全く同じ（アドレスで引くだけ）。どこから読むかはアドレス＝グループで決まり、呼び出し側は指定しません（source-agnostic）。
    /// 本サンプルは「Local アセットしか使わないプロジェクト」想定なので baseUrl を空にしています
    /// （＝version 解決もダウンロードも不要）。Local/Remote 混在アプリでは実 baseUrl を渡しても、Local アセットは引き続きローカルから読まれます。
    /// </summary>
    public sealed class LocalAssetSample : MonoBehaviour
    {
        [SerializeField] private Image _image;

        // Local フォルダ配下アセットのアドレス（フォルダ相対・拡張子なし）。
        [SerializeField] private string _localSpriteAddress = "Internal/coin";

        private readonly AddressablesAssetVaultService _assetVaultService = new();

        private void Start()
        {
            RunAsync(destroyCancellationToken).Forget();
        }

        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Local 専用: baseUrl は空でよい（version.json 解決もダウンロードもスキップされる）。
                await _assetVaultService.InitializeAsync(string.Empty, cancellationToken);

                // ロードは Remote と同一 API。アドレスで引くだけ。GameObject 破棄で自動 Release。
                _image.sprite = await _assetVaultService.LoadAssetAsync<Sprite>(this, _localSpriteAddress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 画面破棄による正常キャンセル。
            }
            catch (AssetVaultException exception)
            {
                Debug.LogError(exception);
            }
        }

        private void OnDestroy()
        {
            // 自前 new した Service を破棄（ロードした asset は GameObject 破棄連動で自動解放）。
            _assetVaultService.Dispose();
        }
    }
}
