using System.Threading;

namespace UniLab.AssetVault
{
    /// <summary>
    /// owner の破棄トークンと <see cref="IAssetScope"/> を束ねるホルダーです。
    /// <see cref="AssetVaultServiceExtensions"/> が明示的に生成し、owner 破棄時にスコープを Dispose します。
    /// </summary>
    internal sealed class AssetScopeHolder
    {
        private readonly CancellationTokenRegistration _disposeRegistration;

        /// <summary>owner に紐づくスコープ。</summary>
        public IAssetScope Scope { get; }

        /// <summary>owner の破棄を表すトークン。</summary>
        public CancellationToken OwnerDestroyToken { get; }

        /// <summary>
        /// owner 破棄時にスコープを解放するホルダーを生成する。
        /// </summary>
        public AssetScopeHolder(IAssetScope scope, CancellationToken ownerDestroyToken)
        {
            Scope = scope;
            OwnerDestroyToken = ownerDestroyToken;
            _disposeRegistration = ownerDestroyToken.Register(scope.Dispose);
        }
    }
}
