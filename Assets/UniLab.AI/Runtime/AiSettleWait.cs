#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniLab.AI
{
    /// <summary>入力とシーン遷移後の静止時間を実時間で追跡します。</summary>
    internal sealed class AiSettleWait : IDisposable
    {
        private readonly float _startedAt;
        private readonly float _settleSeconds;
        private readonly float _timeoutSeconds;
        private float _quietSince;
        internal bool Settled { get; private set; }

        internal AiSettleWait(AiCommandArguments arguments)
        {
            ValidateDuration(arguments.settleSeconds, nameof(arguments.settleSeconds), false);
            ValidateDuration(arguments.settleTimeoutSeconds, nameof(arguments.settleTimeoutSeconds), true);
            _settleSeconds = arguments.settleSeconds;
            _timeoutSeconds = arguments.settleTimeoutSeconds;
            _startedAt = Time.realtimeSinceStartup;
            _quietSince = _startedAt;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        internal IEnumerator Wait()
        {
            // 入力直後は遷移の開始がまだ見えないため、最低一度はフレームを進める。
            yield return null;
            while (Time.realtimeSinceStartup - _startedAt < _timeoutSeconds)
            {
                var now = Time.realtimeSinceStartup;
                if (AgentSessionCommands.IsInputBusy() || HasLoadingScene())
                {
                    _quietSince = now;
                    yield return null;
                    continue;
                }

                if (now - _quietSince >= _settleSeconds)
                {
                    Settled = true;
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>キャンセル時にもシーンイベントの購読を解放します。</summary>
        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _quietSince = Time.realtimeSinceStartup;
        }

        private static bool HasLoadingScene()
        {
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                if (!SceneManager.GetSceneAt(sceneIndex).isLoaded)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateDuration(float seconds, string name, bool requirePositive)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f || (requirePositive && seconds == 0f))
            {
                throw new ArgumentOutOfRangeException(name, "待機秒数が不正です。");
            }
        }
    }
}
#endif
