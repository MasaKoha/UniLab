using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// 入力遮断用 Canvas の表示／非表示を切り替える基底。利用側で派生し、常駐オブジェクトに載せて DI 登録する。
    /// </summary>
    public abstract class UIInputBlockingManagerBase : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas = null;

        /// <summary>遮断に使う Canvas。派生クラスが表示切り替えで参照する。</summary>
        protected Canvas Canvas => _canvas;

        /// <summary>遮断を表示する。</summary>
        public abstract void Show();

        /// <summary>遮断を解除する。</summary>
        public abstract void Hide();
    }
}
