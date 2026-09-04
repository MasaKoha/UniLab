#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>Unity のフレーム上でファイル要求を一件ずつ処理します。</summary>
    public sealed class AiMailboxServer : MonoBehaviour
    {
        private const string PrefabResourcePath = "AiMailboxPrefab";
        private const string EnabledMarker = ".enabled";
        private const float DefaultPollIntervalSeconds = 0.05f;
        private static AiMailboxServer _instance;

        [SerializeField] private float _pollIntervalSeconds = DefaultPollIntervalSeconds;
        private string _directory;
        private string _requestPath;
        private AiCommandRequest _request;
        private AiCommandResponse _pendingResponse;
        private IEnumerator _execution;
        private float _nextPollAt;
        private int _handledCount;

        /// <summary>サーバーが要求を受け付けているかを示します。</summary>
        public static bool IsRunning => _instance != null;
        /// <summary>現在のメールボックス、または停止中の既定パスです。</summary>
        public static string Directory => IsRunning ? _instance._directory : DefaultDirectory;
        /// <summary>現在の起動で応答を書き終えた要求数です。</summary>
        public static int HandledCount => IsRunning ? _instance._handledCount : 0;
        /// <summary>プロジェクトルートを基準にした既定パスです。</summary>
        public static string DefaultDirectory => Path.Combine(DebugOutputPath.DirectoryPath, "agent-mailbox");

        /// <summary>Play 中に共通 Prefab から開始します。既に起動済みならそのまま維持します。</summary>
        public static void Start(string directory = null)
        {
            if (IsRunning)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("playMode が必要です");
            }

            var resolvedDirectory = Path.GetFullPath(string.IsNullOrEmpty(directory) ? DefaultDirectory : directory);
            System.IO.Directory.CreateDirectory(resolvedDirectory);
            AiMailboxFiles.CleanupResponses(resolvedDirectory);
            var prefab = Resources.Load<AiMailboxPrefab>(PrefabResourcePath);
            if (prefab == null)
            {
                throw new InvalidOperationException("AiMailboxServer Prefab がありません。");
            }

            _instance = Instantiate(prefab.Server);
            _instance._directory = resolvedDirectory;
            DontDestroyOnLoad(_instance.gameObject);
        }

        /// <summary>処理中の要求に停止応答を書いてからサーバーを破棄します。</summary>
        public static void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            var instance = _instance;
            instance.StopPendingRequest();
            _instance = null;
            Destroy(instance.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfEnabled()
        {
            if (File.Exists(Path.Combine(DefaultDirectory, EnabledMarker)))
            {
                Start(DefaultDirectory);
            }
        }

        private void Update()
        {
            if (_instance != this || Time.realtimeSinceStartup < _nextPollAt)
            {
                return;
            }

            _nextPollAt = Time.realtimeSinceStartup + Mathf.Max(DefaultPollIntervalSeconds, _pollIntervalSeconds);
            try
            {
                Poll();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"[AiMailboxServer] {exception.Message}");
            }
        }

        private void Poll()
        {
            if (_pendingResponse != null)
            {
                WriteResponse();
                return;
            }

            if (_requestPath != null)
            {
                return;
            }

            // perf: ディレクトリ走査の配列確保はポーリング間隔ごとに限定する。
            var paths = AiMailboxFiles.GetRequests(_directory);
            if (paths.Length == 0)
            {
                return;
            }

            _requestPath = paths[0];
            if (File.Exists(AiMailboxFiles.GetResponsePath(_requestPath)))
            {
                File.Delete(_requestPath);
                ClearRequest();
                return;
            }

            BeginRequest();
        }

        private void BeginRequest()
        {
            try
            {
                _request = AiMailboxFiles.ReadRequest(_requestPath);
            }
            catch (Exception exception)
            {
                OnCompleted(new AiCommandResponse { error = exception.Message });
                return;
            }

            _execution = AiCommandDispatcher.ExecuteAsync(_request, OnCompleted);
            StartCoroutine(_execution);
        }

        private void OnCompleted(AiCommandResponse response)
        {
            _pendingResponse = response;
            // 次の走査を待たずに応答を公開し、入出力の往復遅延を抑える。
            try
            {
                WriteResponse();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"[AiMailboxServer] 応答を再試行します: {exception.Message}");
            }
        }

        private void WriteResponse()
        {
            AiMailboxFiles.Complete(_requestPath, _pendingResponse);
            _handledCount++;
            ClearRequest();
        }

        private void ClearRequest()
        {
            _requestPath = null;
            _request = null;
            _pendingResponse = null;
            _execution = null;
        }

        private void StopPendingRequest()
        {
            StopAllCoroutines();
            // Unity のコルーチン停止だけに依存せず、シーン監視の finally を実行する。
            (_execution as IDisposable)?.Dispose();
            _execution = null;
            if (_requestPath == null)
            {
                return;
            }

            _pendingResponse = new AiCommandResponse { op = _request?.op ?? string.Empty, error = "server stopped" };
            WriteResponse();
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            try
            {
                StopPendingRequest();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"[AiMailboxServer] 停止応答を書けませんでした: {exception.Message}");
            }
            finally
            {
                _instance = null;
            }
        }
    }
}
#endif
