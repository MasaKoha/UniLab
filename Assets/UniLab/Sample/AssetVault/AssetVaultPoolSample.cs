using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.AssetVault.Sample
{
    /// <summary>
    /// オブジェクトプール × AssetVault のサンプルです。
    ///   - プレハブ“資産”は LoadAssetAsync&lt;GameObject&gt;(this) でプール寿命に保持（InstantiateAsync は使わない）
    ///   - インスタンスは自前 Instantiate でプール管理（Get/Return はアクティブ切替のみ＝再ロードしない）
    ///   - 要素の表示スプライトは AssetSlot で差し替え（溜まらない）＋ 共有キャッシュ（TTL/LRU）で churn 回避
    ///   - プール破棄時にプレハブ資産・キャッシュ・Service をまとめて解放
    /// 自己完結のため Service / Cache を直接 new しています（実プロジェクトは DI 注入）。
    /// </summary>
    public sealed class AssetVaultPoolSample : MonoBehaviour
    {
        [Header("配信先 BaseUrl（Local のみなら空でよい）")]
        [SerializeField] private string _baseUrl = "";

        [Header("プール対象プレハブのアドレス（PooledIcon + Image を持つプレハブ）")]
        [SerializeField] private string _itemPrefabAddress = "UI/IconItem";

        [Header("差し替えに使うスプライトのアドレス（順に切り替える）")]
        [SerializeField] private string[] _spriteAddresses = { "Icons/coin", "Icons/gem" };

        [Header("生成先・初期プールサイズ")]
        [SerializeField] private Transform _content;
        [SerializeField] private int _initialPoolSize = 5;

        private readonly AddressablesAssetVaultService _assetVaultService = new();
        private readonly AssetVaultCache _assetVaultCache = new();
        private readonly Stack<PooledIcon> _inactiveItems = new();

        private GameObject _itemPrefab;

        private void Start()
        {
            RunAsync(destroyCancellationToken).Forget();
        }

        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _assetVaultService.InitializeAsync(_baseUrl, cancellationToken);

                // プレハブ資産は owner=this(プール) でロード＝プール GameObject 破棄まで解放されない。
                _itemPrefab = await _assetVaultService.LoadAssetAsync<GameObject>(this, _itemPrefabAddress, cancellationToken);

                for (var index = 0; index < _initialPoolSize; index++)
                {
                    _inactiveItems.Push(CreateItem());
                }

                // デモ: スプライトを切り替えながら Get→表示→Return を繰り返す（再ロードは起きない）。
                for (var step = 0; step < _spriteAddresses.Length * 2; step++)
                {
                    var item = Get();
                    await item.SetIconAsync(_spriteAddresses[step % _spriteAddresses.Length], cancellationToken);
                    await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: cancellationToken);
                    Return(item);
                }
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

        private PooledIcon CreateItem()
        {
            var instance = Instantiate(_itemPrefab, _content);
            var item = instance.GetComponent<PooledIcon>();
            item.Initialize(_assetVaultCache);
            instance.SetActive(false);
            return item;
        }

        private PooledIcon Get()
        {
            var item = _inactiveItems.Count > 0 ? _inactiveItems.Pop() : CreateItem();
            item.gameObject.SetActive(true);
            return item;
        }

        private void Return(PooledIcon item)
        {
            // 破棄せず非アクティブで戻すだけ。スプライトは保持され、再 Get で再ロードしない。
            item.gameObject.SetActive(false);
            _inactiveItems.Push(item);
        }

        private void OnDestroy()
        {
            // プレハブ資産は holder(this) が自動解放。共有スプライトは cache、自前 new した Service を破棄する。
            _assetVaultCache.Dispose();
            _assetVaultService.Dispose();
        }
    }
}
