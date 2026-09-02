#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// 入力可視化オーバーレイの静的入口です。
    /// 録画や外部自動化から 1 行で制御できる呼び出し口を固定します。
    /// </summary>
    public static class InputOverlay
    {
        private static InputOverlayController _controller;

        /// <summary>
        /// 現在表示中かどうかです。
        /// 録画側が自動表示の後始末を判断するために公開します。
        /// </summary>
        public static bool IsVisible
        {
            get
            {
                return _controller != null;
            }
        }

        /// <summary>
        /// オーバーレイを表示します。
        /// 既存インスタンスを再利用することで重複生成と寿命管理の揺れを防ぎます。
        /// </summary>
        public static void Show(InputOverlayOptions options = null)
        {
            if (_controller == null)
            {
                var overlayObject = new GameObject(nameof(InputOverlay));
                Object.DontDestroyOnLoad(overlayObject);
                _controller = overlayObject.AddComponent<InputOverlayController>();
            }

            _controller.Initialize(options ?? new InputOverlayOptions());
        }

        /// <summary>
        /// 互換用の操作ラベル通知です。
        /// 従来のラベル表示は廃止し、履歴帯の疑似操作項目として扱います。
        /// </summary>
        public static void SetStepLabel(string label)
        {
            if (_controller == null)
            {
                return;
            }

            _controller.AddSyntheticHistory(label, Time.realtimeSinceStartup);
        }

        /// <summary>
        /// オーバーレイを消します。
        /// 録画外では既定無効とする設計を保つため明示的に破棄します。
        /// </summary>
        public static void Hide()
        {
            if (_controller == null)
            {
                return;
            }

            var controller = _controller;
            _controller = null;
            Object.Destroy(controller.gameObject);
        }
    }
}
#endif
