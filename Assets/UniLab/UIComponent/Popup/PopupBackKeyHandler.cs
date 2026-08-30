using System;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 「閉じる」入力を購読し、最前面ポップアップの閉じ処理へ橋渡しする。
    /// VContainer の EntryPoint（IStartable）として登録するか、非 DI 環境では Start/Dispose を手動で呼ぶ。
    /// </summary>
    public sealed class PopupBackKeyHandler : IStartable, IDisposable
    {
        private readonly IPopupService _popupService;
        private readonly CompositeDisposable _disposables = new();

        /// <summary>
        /// 閉じる操作の供給元。Android の戻るキー・パッドの Ⓑ・Esc のどれを割り当てるかは
        /// プロジェクト側の責務のため、利用側の LifetimeScope で登録する。
        /// </summary>
        private readonly IPopupBackKeySource _backKeySource;

        /// <summary>閉じ処理を委譲する PopupService と、閉じる入力の供給元を注入する。</summary>
        public PopupBackKeyHandler(IPopupService popupService, IPopupBackKeySource backKeySource)
        {
            _popupService = popupService;
            _backKeySource = backKeySource;
        }

        /// <summary>
        /// バックキー Observable の購読を開始する。VContainer 起動時に自動で、非 DI では手動で呼ぶ。
        /// 実際に閉じるかは Parameter.EnableBackKey / CloseTopAsync 側で判定する。
        /// </summary>
        public void Start()
        {
            _backKeySource.OnBack
                .Subscribe(_ => _popupService.CloseTopAsync().Forget())
                .AddTo(_disposables);
        }

        /// <summary>購読を破棄する。</summary>
        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
