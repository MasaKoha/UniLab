using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UniLab.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace UniLab.Scene
{
    /// <summary>
    /// シーン遷移のライフサイクル（暗転 → 前シーンの退場 → ロード → 新シーンの入場 → 明転）と履歴を管理する基底。
    /// 常駐オブジェクトに載せ、利用側の LifetimeScope で <see cref="ISceneManager"/> として登録する。
    /// シングルトンではないため、所有者が <see cref="Initialize"/> を明示的に呼んでから使う。
    /// </summary>
    public abstract class UniLabSceneManagerBase : MonoBehaviour, ISceneManager, IDisposable
    {
        private readonly Stack<SceneParameterBase> _sceneHistory = new();
        private readonly CompositeDisposable _disposables = new();

        private IBackKeyInput _backKeyInput;
        private LifetimeScope _parentScope;

        /// <summary><see cref="ExecuteBootSequence"/> が完了したか。</summary>
        public bool IsBoot { get; private set; }

        /// <summary>
        /// 前シーンを離れる前に画面を覆う（暗転）。ロード中の画面が見えないようにする。
        /// </summary>
        protected abstract UniTask CoverScreenAsync();

        /// <summary>
        /// 新シーンの準備が済んだあとに覆いを外す（明転）。
        /// </summary>
        protected abstract UniTask RevealScreenAsync();

        /// <summary>
        /// 戻る入力と、各シーンの LifetimeScope に親として継承させるスコープを受け取る。所有者が起動時に一度だけ呼ぶ。
        /// parentScope はシーンロード中に <see cref="LifetimeScope.EnqueueParent"/> で積まれ、
        /// 新シーンの LifetimeScope がアプリ全体の登録（常駐サービス等）を解決できるようにする。
        /// </summary>
        public void Initialize(IBackKeyInput backKeyInput, LifetimeScope parentScope)
        {
            _backKeyInput = backKeyInput;
            _parentScope = parentScope;

            _backKeyInput.OnPressBackKey
                .Subscribe(_ => GoBack())
                .AddTo(_disposables);
        }

        /// <summary>購読を破棄する。</summary>
        public void Dispose()
        {
            _disposables.Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void GoBack()
        {
            // 遷移中は IBackKeyInput 側が発火を止めるが、履歴が無いときの戻るはここで弾く
            if (_sceneHistory.Count <= 1)
            {
                return;
            }

            BackToPreviousScene();
        }

        /// <inheritdoc/>
        public void GoToNextScene(SceneParameterBase sceneParameter, bool addToHistory = false)
        {
            LoadSceneAsync(sceneParameter, addToHistory).Forget();
        }

        /// <summary>LoadSceneMode を明示する版。Additive 読み込みが要る画面で使う。</summary>
        public void GoToNextScene(SceneParameterBase sceneParameter, bool addToHistory, LoadSceneMode mode)
        {
            if (addToHistory)
            {
                _sceneHistory.Push(sceneParameter);
            }

            LoadScene(sceneParameter, CancellationToken.None, mode).Forget();
        }

        /// <inheritdoc/>
        public void BackToPreviousScene()
        {
            if (_sceneHistory.Count <= 1)
            {
                return;
            }

            _sceneHistory.Pop();
            LoadScene(_sceneHistory.Peek(), CancellationToken.None).Forget();
        }

        /// <summary>履歴を空にする。タイトルへ戻るなど、戻り先を断ち切りたいときに呼ぶ。</summary>
        public void ClearHistory()
        {
            _sceneHistory.Clear();
        }

        /// <inheritdoc/>
        public UniTask LoadSceneAsync(
            SceneParameterBase parameter,
            bool addToHistory = false,
            CancellationToken cancellationToken = default)
        {
            if (addToHistory)
            {
                _sceneHistory.Push(parameter);
            }

            return LoadScene(parameter, cancellationToken);
        }

        /// <inheritdoc/>
        public TParameter GetCurrentSceneParameter<TParameter>() where TParameter : SceneParameterBase
        {
            if (_sceneHistory.Count == 0)
            {
                return null;
            }

            var currentSceneParameter = _sceneHistory.Peek();
            if (currentSceneParameter is TParameter parameter)
            {
                return parameter;
            }

            Debug.LogError($"Current scene parameter is not of type {typeof(TParameter)}");
            return null;
        }

        private async UniTask LoadScene(
            SceneParameterBase sceneParameter,
            CancellationToken cancellationToken,
            LoadSceneMode mode = LoadSceneMode.Single)
        {
            var sceneName = sceneParameter.SceneName.ToString();

            // 遷移中の二重遷移を防ぐ。例外・キャンセルで抜けても受付を戻すため finally で解除する
            _backKeyInput.SetBlock(true);
            try
            {
                await LeavePreviousSceneAsync(cancellationToken);
                await CoverScreenAsync().AttachExternalCancellation(cancellationToken);

                // 新シーンの LifetimeScope が生成される瞬間だけ親を積む
                using (LifetimeScope.EnqueueParent(_parentScope))
                {
                    await SceneManager.LoadSceneAsync(sceneName, mode).ToUniTask(cancellationToken: cancellationToken);
                }

                await EnterNewSceneAsync(sceneName, sceneParameter, cancellationToken);
            }
            finally
            {
                _backKeyInput.SetBlock(false);
            }
        }

        /// <summary>前シーンの SceneMainBase があれば退場処理を走らせる。無いシーンではスキップする。</summary>
        private static async UniTask LeavePreviousSceneAsync(CancellationToken cancellationToken)
        {
            var previous = FindSceneMain(SceneManager.GetActiveScene());
            if (previous == null)
            {
                return;
            }

            await previous.TransitionAsync().AttachExternalCancellation(cancellationToken);
            previous.Leave();
        }

        /// <summary>新シーンの SceneMainBase があれば入場処理を走らせる。明転はその準備が済んでから行う。</summary>
        private async UniTask EnterNewSceneAsync(string sceneName, SceneParameterBase sceneParameter, CancellationToken cancellationToken)
        {
            var current = FindSceneMain(SceneManager.GetSceneByName(sceneName));
            if (current == null)
            {
                await RevealScreenAsync().AttachExternalCancellation(cancellationToken);
                return;
            }

            current.SetParameter(sceneParameter);
            current.Setup();
            current.Initialize();
            await current.PreEnterAsync().AttachExternalCancellation(cancellationToken);
            await RevealScreenAsync().AttachExternalCancellation(cancellationToken);
            current.Enter();
        }

        /// <summary>
        /// 起動シーンの入場処理を走らせる。既に実行済みなら何もしない。
        /// </summary>
        public async UniTask ExecuteBootSequence()
        {
            if (IsBoot)
            {
                return;
            }

            var current = FindSceneMain(SceneManager.GetActiveScene());
            if (current != null)
            {
                current.Setup();
                var alreadyInitialized = current.Initialize();
                if (!alreadyInitialized)
                {
                    await current.PreEnterAsync();
                    await RevealScreenAsync();
                    current.Enter();
                    await current.TransitionAsync();
                }
            }

            IsBoot = true;
        }

        /// <summary>
        /// シーンのルートから SceneMainBase を探す。シーンごとに高々1つの前提。
        /// ルート直下の限られた数だけを見るため、遷移時1回の探索コストは許容する。
        /// </summary>
        private static SceneMainBase FindSceneMain(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            foreach (var rootGameObject in scene.GetRootGameObjects())
            {
                var sceneMain = rootGameObject.GetComponent<SceneMainBase>();
                if (sceneMain != null)
                {
                    return sceneMain;
                }
            }

            return null;
        }
    }
}
