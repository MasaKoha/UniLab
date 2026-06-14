using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.AssetVault
{
    /// <summary>
    /// Scope を書かずにアセットをロード／生成するための <see cref="IAssetVaultService"/> 拡張です。
    /// owner（GameObject または Component）の GameObject 寿命にロードが紐づき、その破棄時に Addressables から自動 Release されます。
    /// 画面/シーン単位でまとめたい・共有寿命を制御したい場合は、明示的な <see cref="IAssetVaultService.CreateScope"/> を使ってください。
    /// </summary>
    public static class AssetVaultServiceExtensions
    {
        /// <summary>
        /// key で asset をロードします。ハンドルは owner の GameObject に紐づき、破棄時に自動 Release されます。
        /// cancellationToken 未指定時は owner の GameObject 破棄トークンを使い、ロード中のキャンセルも自動化します。
        /// </summary>
        public static UniTask<T> LoadAssetAsync<T>(this IAssetVaultService service, GameObject owner, string key, CancellationToken cancellationToken = default)
        {
            var holder = AssetScopeHolder.GetOrAttach(owner, service);
            return holder.Scope.LoadAssetAsync<T>(key, ResolveToken(holder, cancellationToken));
        }

        /// <summary>
        /// <see cref="LoadAssetAsync{T}(IAssetVaultService,GameObject,string,CancellationToken)"/> の Component 版です。
        /// </summary>
        public static UniTask<T> LoadAssetAsync<T>(this IAssetVaultService service, Component owner, string key, CancellationToken cancellationToken = default)
        {
            return service.LoadAssetAsync<T>(owner.gameObject, key, cancellationToken);
        }

        /// <summary>
        /// key で GameObject を生成します。生成物のハンドルは owner の GameObject に紐づき、破棄時に自動 Release（生成物も破棄）されます。
        /// </summary>
        public static UniTask<GameObject> InstantiateAsync(this IAssetVaultService service, GameObject owner, string key, Transform parent, CancellationToken cancellationToken = default)
        {
            var holder = AssetScopeHolder.GetOrAttach(owner, service);
            return holder.Scope.InstantiateAsync(key, parent, ResolveToken(holder, cancellationToken));
        }

        /// <summary>
        /// <see cref="InstantiateAsync(IAssetVaultService,GameObject,string,Transform,CancellationToken)"/> の Component 版です。
        /// </summary>
        public static UniTask<GameObject> InstantiateAsync(this IAssetVaultService service, Component owner, string key, Transform parent, CancellationToken cancellationToken = default)
        {
            return service.InstantiateAsync(owner.gameObject, key, parent, cancellationToken);
        }

        // 未指定なら GameObject 破棄トークン（ホルダーも同じ GameObject の MonoBehaviour）を既定にする。
        private static CancellationToken ResolveToken(AssetScopeHolder holder, CancellationToken cancellationToken)
        {
            return cancellationToken == default ? holder.destroyCancellationToken : cancellationToken;
        }
    }
}
