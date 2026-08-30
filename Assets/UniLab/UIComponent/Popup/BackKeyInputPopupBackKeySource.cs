using R3;
using UniLab.Input;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// <see cref="IBackKeyInput"/>（戻るキー）をポップアップの「閉じる操作」として流用する標準アダプタ。
    /// 戻るキーでポップアップを閉じたいプロジェクトは、これを <see cref="IPopupBackKeySource"/> として登録する。
    /// パッドの Ⓑ など別の入力に割り当てたい場合は独自実装を登録する。
    /// </summary>
    public sealed class BackKeyInputPopupBackKeySource : IPopupBackKeySource
    {
        private readonly IBackKeyInput _backKeyInput;

        /// <summary>閉じる操作の元になる戻るキー入力を受け取る。</summary>
        public BackKeyInputPopupBackKeySource(IBackKeyInput backKeyInput)
        {
            _backKeyInput = backKeyInput;
        }

        /// <inheritdoc/>
        public Observable<Unit> OnBack => _backKeyInput.OnPressBackKey;
    }
}
