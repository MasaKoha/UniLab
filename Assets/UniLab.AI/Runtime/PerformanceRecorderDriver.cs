#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// 依存追加なしで毎フレーム採取するため、明示生成した一時オブジェクトだけに Update を閉じ込める。
    /// </summary>
    public sealed class PerformanceRecorderDriver : MonoBehaviour
    {
        private PerformanceRecorder _owner;

        /// <summary>
        /// 監視対象の寿命を録画開始/停止へ一致させ、常駐オブジェクトを増やさない。
        /// </summary>
        public static PerformanceRecorderDriver Create(PerformanceRecorder owner)
        {
            var driverObject = new GameObject(nameof(PerformanceRecorderDriver));
            DontDestroyOnLoad(driverObject);
            driverObject.hideFlags = HideFlags.HideAndDontSave;

            var driver = driverObject.AddComponent<PerformanceRecorderDriver>();
            driver._owner = owner;
            return driver;
        }

        /// <summary>
        /// 停止時にのみ破棄させ、計測の生存期間を呼び出し元が明示制御できるようにする。
        /// </summary>
        public void DestroySelf()
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (_owner == null)
            {
                return;
            }

            _owner.SampleFrame();
        }

        private void OnDestroy()
        {
            if (_owner == null)
            {
                return;
            }

            _owner.HandleDriverDestroyed();
            _owner = null;
        }
    }
}
#endif
