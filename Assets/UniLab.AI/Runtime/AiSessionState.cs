#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// AI ツール実行中かどうかを参照カウントで公開します。
    /// 利用側はこの状態を購読してデバッグ UI や入力制御を切り替えます。
    /// </summary>
    public static class AiSessionState
    {
        private static int _activeCount;

        /// <summary>
        /// 1 つ以上の AI セッションが動作中なら true を返します。
        /// </summary>
        public static bool IsActive
        {
            get { return _activeCount > 0; }
        }

        /// <summary>
        /// AI セッション稼働状態の変化を通知します。
        /// </summary>
        public static event Action<bool> ActiveChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _activeCount = 0;
            ActiveChanged = null;
        }

        internal static void Enter(string source)
        {
            var previousCount = _activeCount;
            _activeCount++;
            if (previousCount == 0)
            {
                ActiveChanged?.Invoke(true);
            }
        }

        internal static void Exit(string source)
        {
            if (_activeCount <= 0)
            {
                UnityEngine.Debug.LogWarning($"[AiSessionState] Exit が過剰です。 source={source}");
                _activeCount = 0;
                return;
            }

            _activeCount--;
            if (_activeCount == 0)
            {
                ActiveChanged?.Invoke(false);
            }
        }
    }
}
#endif
