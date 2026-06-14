using UnityEngine;

namespace UniLab.AssetVault
{
    /// <summary>
    /// GameObject に 1 つだけ付く、その GameObject 寿命に紐づく <see cref="IAssetScope"/> の隠しホルダーです。
    /// <see cref="AssetVaultComponentExtensions"/> が裏で付与し、GameObject 破棄時にスコープを Dispose（＝ロード済み asset を一括 Release）します。
    /// アプリコードが直接触る必要はありません。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class AssetScopeHolder : MonoBehaviour
    {
        private IAssetScope _scope;

        /// <summary>この GameObject に紐づくスコープ。初回アクセスで生成します。</summary>
        public IAssetScope Scope => _scope ??= new AssetScope();

        /// <summary>
        /// GameObject に付いているホルダーを取得し、無ければ付与します（hideFlags で Inspector からは隠します）。
        /// </summary>
        public static AssetScopeHolder GetOrAttach(GameObject gameObject)
        {
            if (!gameObject.TryGetComponent(out AssetScopeHolder holder))
            {
                holder = gameObject.AddComponent<AssetScopeHolder>();
                holder.hideFlags = HideFlags.HideInInspector;
            }

            return holder;
        }

        private void OnDestroy()
        {
            // GameObject 破棄＝この寿命でロードした全 asset を解放する。
            _scope?.Dispose();
            _scope = null;
        }
    }
}
