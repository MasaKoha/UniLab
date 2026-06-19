namespace UniLab.UI.Popup
{
    /// <summary>
    /// 型付きパラメータ TParameter と結果型 TResult を持つポップアップ View の基底。
    /// 派生クラスは OnSetup で型付きパラメータから UI を構築する。旧 PopupBaseWrapper を統合したもの。
    /// </summary>
    public abstract class PopupBase<TParameter, TResult> : PopupBase<TResult>
        where TParameter : IPopupParameter
    {
        /// <summary>型付きの表示パラメータ。基底の IPopupParameter を TParameter として公開する。</summary>
        protected new TParameter Parameter => (TParameter)base.Parameter;

        protected sealed override void OnInitialize()
        {
            OnSetup(Parameter);
        }

        /// <summary>型付きパラメータで UI を構築する。Initialize 時に呼ばれる。</summary>
        protected abstract void OnSetup(TParameter parameter);
    }
}
