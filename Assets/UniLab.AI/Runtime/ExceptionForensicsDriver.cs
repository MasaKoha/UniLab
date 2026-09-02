#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// スレッド外ログをメインスレッドで収集するため、Unity API に触れる地点を Update へ寄せます。
    /// </summary>
    public sealed class ExceptionForensicsDriver : MonoBehaviour
    {
        private ExceptionForensics _forensics;

        /// <summary>
        /// 所有者を明示し、破棄時に古い参照で収集しないようにします。
        /// </summary>
        public void Initialize(ExceptionForensics forensics)
        {
            _forensics = forensics;
        }

        /// <summary>
        /// 所有者破棄時に参照を外し、次の Play へ状態を残さないようにします。
        /// </summary>
        public void Clear()
        {
            _forensics = null;
        }

        private void Update()
        {
            _forensics?.CapturePending();
        }
    }
}
#endif
