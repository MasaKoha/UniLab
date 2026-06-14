using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.AssetVault.Sample
{
    /// <summary>
    /// プール要素のサンプル。再利用ごとに表示スプライトが変わる前提で、AssetSlot で「1スロット差し替え」管理します。
    /// auto-holder（service.LoadAssetAsync(this, ...)）だと差し替え分が GameObject 寿命まで溜まるため、動的差し替えは AssetSlot を使う。
    /// </summary>
    public sealed class PooledIcon : MonoBehaviour
    {
        [SerializeField] private Image _image;

        private AssetSlot<Sprite> _iconSlot;

        /// <summary>プール生成時に1回呼び、共有キャッシュを渡してスロットを用意します（非 DI サンプルのため明示注入）。</summary>
        public void Initialize(IAssetVaultCache cache)
        {
            _iconSlot ??= new AssetSlot<Sprite>(cache);
        }

        /// <summary>表示スプライトを差し替えます。旧スプライトは解放され、新スプライトを取得します（溜まらない）。</summary>
        public async UniTask SetIconAsync(string spriteKey, CancellationToken cancellationToken)
        {
            _image.sprite = await _iconSlot.SetAsync(spriteKey, cancellationToken);
        }

        private void OnDestroy()
        {
            // プール破棄（この GameObject 破棄）でスロットの現参照を解放する。
            _iconSlot?.Dispose();
        }
    }
}
