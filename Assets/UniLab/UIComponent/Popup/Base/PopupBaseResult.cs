using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 結果型 TResult を持つポップアップ View の基底。結果解決を UniTaskCompletionSource に集約し、
    /// PopupService が GetResultAsync で待機する。任意の結果型に対応するための中間基底。
    /// </summary>
    public abstract class PopupBase<TResult> : PopupBase
    {
        private UniTaskCompletionSource<TResult> _resultSource = new();

        /// <summary>前回の結果を捨て、今回の表示用に結果待ちを作り直す。</summary>
        protected override void OnPrepare()
        {
            _resultSource = new UniTaskCompletionSource<TResult>();
        }

        /// <summary>結果を確定する。ボタン操作や OnClose から呼ぶ。</summary>
        protected void SetResult(TResult result)
        {
            _resultSource.TrySetResult(result);
        }

        /// <summary>結果が確定するまで待つ。PopupService が await する。</summary>
        public UniTask<TResult> GetResultAsync()
        {
            return _resultSource.Task;
        }
    }
}
