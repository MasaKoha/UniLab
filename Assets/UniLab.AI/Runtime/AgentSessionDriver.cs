#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// 同期ブリッジから開始された継続入力を Unity のフレーム進行へ逃がすための最小ドライバです。
    /// </summary>
    public sealed class AgentSessionDriver : MonoBehaviour
    {
        private int _runningCount;

        /// <summary>
        /// 継続入力が残っている間、外部運転手が Observe を待つ判断材料にできます。
        /// </summary>
        public bool IsBusy
        {
            get { return _runningCount > 0; }
        }

        /// <summary>
        /// セッション寿命にひも付いたコルーチンとして開始し、入力デバイス状態の解放漏れを避けます。
        /// </summary>
        public void Run(IEnumerator coroutine)
        {
            if (coroutine == null)
            {
                return;
            }

            StartCoroutine(RunCoroutine(coroutine));
        }

        private IEnumerator RunCoroutine(IEnumerator coroutine)
        {
            _runningCount++;
            while (true)
            {
                object current;
                try
                {
                    if (!coroutine.MoveNext())
                    {
                        break;
                    }

                    current = coroutine.Current;
                }
                catch (System.Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                    break;
                }

                yield return current;
            }

            _runningCount--;
        }
    }
}
#endif
