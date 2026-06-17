using UnityEngine;

namespace UniLab.AssetVault
{
    /// <summary>
    /// リトライ時の指数バックオフ間隔を計算する純ロジックです。
    /// Download / Update のリトライで仕様を共有し、間隔の定義を一箇所に集約します（Addressables/UniTask に依存しないため単体テスト可能）。
    /// </summary>
    internal static class AssetVaultRetryPolicy
    {
        /// <summary>指数バックオフの初期遅延（秒）。即時リトライで相手を叩かないための最小間隔です。</summary>
        internal const float InitialRetryDelaySeconds = 0.5f;

        /// <summary>指数バックオフの上限遅延（秒）。間隔が無制限に伸びてユーザーを待たせ続けるのを防ぐキャップです。</summary>
        internal const float MaxRetryDelaySeconds = 8f;

        /// <summary>
        /// attempt 回目（0 始まり）のリトライ前に待つ秒数を返します。
        /// 0.5s, 1s, 2s, 4s… と倍々で伸ばし、<see cref="MaxRetryDelaySeconds"/> で頭打ちにします。
        /// </summary>
        internal static float GetBackoffDelaySeconds(int attempt)
        {
            return Mathf.Min(InitialRetryDelaySeconds * Mathf.Pow(2f, attempt), MaxRetryDelaySeconds);
        }
    }
}
