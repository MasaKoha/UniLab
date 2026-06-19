using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 結果型 TResult を持つポップアップ View の基底。結果解決を UniTaskCompletionSource に集約し、
    /// PopupService が GetResultAsync で待機する。任意の結果型に対応するための中間基底。
    /// </summary>
    public abstract class PopupBase<TResult> : PopupBase
    {
        private readonly UniTaskCompletionSource<TResult> _resultSource = new();

        /// <summary>結果を確定する。ボタン操作や OnClose から呼ぶ。</summary>
        protected void SetResult(TResult result)
        {
            _resultSource.TrySetResult(result);
        }

        /// <summary>結果が確定するまで待つ。PopupService / マネージャが await する。</summary>
        public UniTask<TResult> GetResultAsync()
        {
            return _resultSource.Task;
        }

        /// <summary>結果確定を待機する。基底で共通実装し、派生での再実装を禁じる。</summary>
        public sealed override async UniTask WaitAsync()
        {
            await _resultSource.Task;
        }
    }
}
