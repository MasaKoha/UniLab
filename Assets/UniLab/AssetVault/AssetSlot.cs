using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.AssetVault
{
    /// <summary>
    /// 「1 つのアセットを差し込むスロット」です。<see cref="SetAsync"/> で key を差し替えると、
    /// 旧アセットを解放してから新アセットを取得します（常に最大1参照しか持たないため溜まりません）。
    /// オブジェクトプール要素のように「再利用ごとに表示アセットが変わる」ケースで、スコープ手動管理の代わりに使います。
    /// Dispose で現在の参照を解放します。<see cref="IAssetVaultCache"/> 経由なので共有・遅延解放（TTL/LRU）の恩恵も受けます。
    /// </summary>
    public sealed class AssetSlot<T> : IDisposable
    {
        private readonly IAssetVaultCache _cache;
        private IAssetReference<T> _current;
        private string _currentKey;
        private CancellationTokenSource _cancellationTokenSource;

        public AssetSlot(IAssetVaultCache cache)
        {
            _cache = cache;
        }

        /// <summary>現在保持しているアセット。未設定時は default。</summary>
        public T Value => _current != null ? _current.Value : default;

        /// <summary>
        /// スロットの key を差し替えます。同じ key なら何もしません。空 key はクリア（解放のみ）。
        /// 連続呼び出しは前回の取得をキャンセルして最新だけを反映します。
        /// </summary>
        public async UniTask<T> SetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_currentKey == key)
            {
                return Value;
            }

            // 前回の進行中 SetAsync をキャンセルし、今回分のトークンを用意する。
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cancellationTokenSource.Token;

            // 旧参照を即解放（参照カウントを手放す。キャッシュ側で TTL 猶予保持される）。
            _current?.Dispose();
            _current = null;
            _currentKey = key;

            if (string.IsNullOrEmpty(key))
            {
                return default;
            }

            var reference = await _cache.AcquireAsync<T>(key, token);

            // await 中にさらに差し替えられた/破棄された場合は、取得分を捨てて最新を尊重する。
            if (token.IsCancellationRequested)
            {
                reference.Dispose();
                return default;
            }

            _current = reference;
            return reference.Value;
        }

        /// <summary>現在のアセットを解放してスロットを空にします。</summary>
        public void Clear()
        {
            _current?.Dispose();
            _current = null;
            _currentKey = null;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            Clear();
        }
    }
}
