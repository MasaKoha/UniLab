using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.AssetVault
{
    /// <summary>
    /// Component から Scope を意識せずにアセットをロード／生成するための拡張です。
    /// ロードは呼び出し元 Component の GameObject 寿命に自動で紐づき、その GameObject 破棄時に Addressables から解放されます。
    /// 画面/シーン単位でまとめたい、または共有アセットなど上位の寿命管理が要る場合は、明示的な <see cref="IAssetVaultService.CreateScope"/> を使ってください。
    /// </summary>
    public static class AssetVaultComponentExtensions
    {
        /// <summary>
        /// key で asset をロードします。ハンドルは GameObject に紐づき、破棄時に自動 Release されます。
        /// cancellationToken 未指定時は GameObject の破棄トークンを使い、ロード中のキャンセルも自動化します。
        /// </summary>
        public static UniTask<T> LoadAssetAsync<T>(this Component component, string key, CancellationToken cancellationToken = default)
        {
            var holder = AssetScopeHolder.GetOrAttach(component.gameObject);
            return holder.Scope.LoadAssetAsync<T>(key, ResolveToken(holder, cancellationToken));
        }

        /// <summary>
        /// key で GameObject を生成します。生成物のハンドルは呼び出し元 GameObject に紐づき、破棄時に自動 Release（生成物も破棄）されます。
        /// </summary>
        public static UniTask<GameObject> InstantiateAsync(this Component component, string key, Transform parent, CancellationToken cancellationToken = default)
        {
            var holder = AssetScopeHolder.GetOrAttach(component.gameObject);
            return holder.Scope.InstantiateAsync(key, parent, ResolveToken(holder, cancellationToken));
        }

        // 未指定なら GameObject 破棄トークン（ホルダーも同じ GameObject の MonoBehaviour）を既定にする。
        private static CancellationToken ResolveToken(AssetScopeHolder holder, CancellationToken cancellationToken)
        {
            return cancellationToken == default ? holder.destroyCancellationToken : cancellationToken;
        }
    }
}
