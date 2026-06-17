using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.AssetVault
{
    /// <summary>
    /// AssetVaultException を指数バックオフでリトライしながら非同期操作を実行する共通ヘルパーです。
    /// 初期化・カタログ更新・ダウンロードに散在していたリトライループを一本化します（間隔計算は <see cref="AssetVaultRetryPolicy"/> に委譲）。
    /// </summary>
    internal static class AssetVaultRetry
    {
        /// <summary>
        /// operation を実行し、<see cref="AssetVaultException"/> が出たら最大 maxRetryCount 回まで指数バックオフでリトライします。
        /// 成功すればその結果を返し、全試行が失敗したら最後の例外を再 throw します。
        /// OperationCanceledException はリトライ対象にせず、そのまま呼び出し側へ伝播します。
        /// </summary>
        internal static async UniTask<T> RunAsync<T>(
            Func<CancellationToken, UniTask<T>> operation,
            int maxRetryCount,
            CancellationToken cancellationToken)
        {
            AssetVaultException lastException = null;

            for (var attempt = 0; attempt <= maxRetryCount; attempt++)
            {
                try
                {
                    return await operation(cancellationToken);
                }
                catch (AssetVaultException exception)
                {
                    // 通信断など再試行で復帰し得るエラー。最後の試行なら抜けて呼び出し側へ再 throw する。
                    lastException = exception;
                    if (attempt >= maxRetryCount)
                    {
                        break;
                    }

                    var delaySeconds = AssetVaultRetryPolicy.GetBackoffDelaySeconds(attempt);
                    await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: cancellationToken);
                }
            }

            throw lastException;
        }

        /// <summary>
        /// 戻り値の無い操作向けのオーバーロードです。リトライ挙動は <see cref="RunAsync{T}"/> と同じです。
        /// </summary>
        internal static async UniTask RunAsync(
            Func<CancellationToken, UniTask> operation,
            int maxRetryCount,
            CancellationToken cancellationToken)
        {
            await RunAsync(
                async token =>
                {
                    await operation(token);
                    return true;
                },
                maxRetryCount,
                cancellationToken);
        }
    }
}
