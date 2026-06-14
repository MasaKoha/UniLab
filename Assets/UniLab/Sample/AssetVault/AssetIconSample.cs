using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.AssetVault.Sample
{
    /// <summary>
    /// Scope も Dispose も CancellationToken も書かない、最小のロードサンプルです。
    /// this.LoadAssetAsync 拡張により、この GameObject の破棄で読み込んだ asset が自動 Release されます。
    /// 事前条件: 別途 IAssetVaultService.InitializeAsync 済みであること（アプリ起動シーケンスで実施）。
    /// </summary>
    public sealed class AssetIconSample : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private string _spriteAddress = "Icons/coin";

        private void Start()
        {
            ShowAsync().Forget();
        }

        private async UniTask ShowAsync()
        {
            // Scope 不要。ロードは this（GameObject）の寿命に自動で紐づく。
            // cancellationToken 省略時は destroyCancellationToken が使われ、破棄でロードもキャンセルされる。
            _image.sprite = await this.LoadAssetAsync<Sprite>(_spriteAddress);
        }
    }
}
