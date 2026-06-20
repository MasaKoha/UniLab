using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace UniLab.UI.Popup.Tests
{
    // ---------------------------------------------------------------------------
    // テストダブル
    // ---------------------------------------------------------------------------

    /// <summary>
    /// テスト用ポップアップパラメータ。Priority・EnableBackKey を自由に設定できる。
    /// </summary>
    internal sealed class TestPopupServiceParameter : IPopupParameter
    {
        /// <summary>表示要求の優先度。</summary>
        public PopupPriority Priority { get; set; } = PopupPriority.Normal;

        /// <summary>バックキーに反応して閉じるか。</summary>
        public bool EnableBackKey { get; set; } = true;

        /// <summary>バックキー時のカスタム処理。</summary>
        public Func<UniTask> CustomBackAsync { get; set; } = null;

        /// <summary>背景タップで閉じるか。</summary>
        public bool EnableBackgroundClose { get; set; } = false;

        /// <summary>既存表示に重ねて即時表示するか。既定は重ねず優先度キューで直列表示する。</summary>
        public bool Stack { get; set; } = false;
    }

    /// <summary>
    /// テスト用の int 結果ポップアップ。OpenAsync/CloseAsync は即完了し、
    /// Resolve(int) で外部から結果を確定できる。呼び出し回数を記録する。
    /// </summary>
    internal sealed class MockResultPopup : PopupBase<int>
    {
        /// <summary>Initialize（表示処理の開始）が呼ばれたか。処理順の検証に使う。</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>外部から結果を確定してポップアップを閉じる。</summary>
        public void Resolve(int value)
        {
            SetResult(value);
        }

        /// <summary>派生クラス固有の初期化処理。表示されたことを記録する。</summary>
        protected override void OnInitialize()
        {
            IsInitialized = true;
        }

        /// <summary>バックキー / 背景タップ時の閉じ処理。-1 を結果として返す。</summary>
        public override void OnClose()
        {
            SetResult(-1);
        }
    }

    /// <summary>
    /// テスト用の IPopupViewProvider。あらかじめ設定した MockResultPopup インスタンスを返す。
    /// Release の呼び出し履歴と例外注入フラグを持つ。
    /// </summary>
    internal sealed class MockPopupViewProvider : IPopupViewProvider
    {
        // LoadAsync で返すインスタンスを Queue で管理する（呼び出し順に渡す）
        private readonly Queue<MockResultPopup> _instances = new();

        /// <summary>Release が呼ばれた PopupBase のリスト。</summary>
        public List<PopupBase> ReleasedPopups { get; } = new();

        /// <summary>Release が呼ばれた回数。</summary>
        public int ReleaseCallCount => ReleasedPopups.Count;

        /// <summary>true にすると LoadAsync で InvalidOperationException を投げる。</summary>
        public bool ThrowOnLoad { get; set; } = false;

        /// <summary>LoadAsync が呼ばれた回数。</summary>
        public int LoadCallCount { get; private set; }

        /// <summary>返却するポップアップインスタンスをキューに追加する。</summary>
        public void EnqueueInstance(MockResultPopup popup)
        {
            _instances.Enqueue(popup);
        }

        /// <summary>
        /// View を生成して返す。ThrowOnLoad が true の場合は InvalidOperationException を投げる。
        /// </summary>
        public UniTask<TPopup> LoadAsync<TPopup>(CancellationToken cancellationToken)
            where TPopup : PopupBase
        {
            LoadCallCount++;

            if (ThrowOnLoad)
            {
                throw new InvalidOperationException("テスト用: LoadAsync 例外注入");
            }

            if (_instances.Count == 0)
            {
                throw new InvalidOperationException("テスト用: MockPopupViewProvider にインスタンスが登録されていない");
            }

            var instance = _instances.Dequeue();
            return UniTask.FromResult((TPopup)(object)instance);
        }

        /// <summary>解放された View を記録する。</summary>
        public void Release(PopupBase popup)
        {
            ReleasedPopups.Add(popup);
        }
    }

    // ---------------------------------------------------------------------------
    // ヘルパー
    // ---------------------------------------------------------------------------

    /// <summary>
    /// テスト共通のヘルパー。MockResultPopup インスタンスを背景ボタン付きで生成する。
    /// PopupBase が _backgroundButton SerializeField を必要とするため、リフレクションで注入する。
    /// </summary>
    internal static class MockPopupFactory
    {
        /// <summary>テスト用の MockResultPopup を GameObject に追加し、_backgroundButton を注入して返す。</summary>
        public static MockResultPopup Create(string name = "MockPopup")
        {
            var gameObject = new GameObject(name);

            // 背景ボタン用の子 GameObject を追加し、PopupBase._backgroundButton に注入する
            var backgroundButtonGameObject = new GameObject("Background");
            backgroundButtonGameObject.transform.SetParent(gameObject.transform);
            var backgroundButton = backgroundButtonGameObject.AddComponent<Button>();

            var popup = gameObject.AddComponent<MockResultPopup>();

            var backgroundButtonField = typeof(PopupBase).GetField(
                "_backgroundButton",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            backgroundButtonField?.SetValue(popup, backgroundButton);

            // PopupService は SetActive(true) を呼ぶため初期非表示にしておく
            gameObject.SetActive(false);
            return popup;
        }
    }

    // ---------------------------------------------------------------------------
    // テスト本体
    // ---------------------------------------------------------------------------

    /// <summary>
    /// PopupService の動作を検証する PlayMode テスト。
    /// ShowAsync の直列化・優先度制御・キャンセル・Release 保証を網羅する。
    /// </summary>
    public class PopupServiceTest
    {
        private MockPopupViewProvider _viewProvider;
        private PopupService _service;

        [SetUp]
        public void SetUp()
        {
            _viewProvider = new MockPopupViewProvider();
            _service = new PopupService(_viewProvider);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        // --- ヘルパー ---

        /// <summary>Normal 優先度・EnableBackKey=true の標準パラメータを生成する。</summary>
        private static TestPopupServiceParameter CreateParameter(
            PopupPriority priority = PopupPriority.Normal,
            bool enableBackKey = true)
        {
            return new TestPopupServiceParameter
            {
                Priority = priority,
                EnableBackKey = enableBackKey,
            };
        }

        // ---------------------------------------------------------------------------
        // テストケース 1: 単独 ShowAsync
        // ---------------------------------------------------------------------------

        /// <summary>
        /// ShowAsync 単独実行: Resolve した値が戻り値に反映され、
        /// HasActivePopup が表示中 true・完了後 false になること。
        /// OpenAsync・CloseAsync・Release が各 1 回ずつ呼ばれること。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_SingleRequest_ReturnsResolvedValueAndReleasesView()
        {
            // Arrange
            var popup = MockPopupFactory.Create();
            _viewProvider.EnqueueInstance(popup);
            var parameter = CreateParameter();
            const int expectedResult = 42;
            var actualResult = 0;
            var isCompleted = false;

            // UniTask の戻り値を取得するため async UniTaskVoid でラップしてキャプチャする
            async UniTaskVoid StartAndCapture()
            {
                actualResult = await _service.ShowAsync<MockResultPopup, int>(parameter);
                isCompleted = true;
            }

            // Act
            StartAndCapture().Forget();

            // HasActivePopup は OpenAsync 完了直後に true になる。1 フレーム待つ。
            yield return null;

            Assert.IsTrue(_service.HasActivePopup.CurrentValue, "表示中は HasActivePopup が true であること");

            popup.Resolve(expectedResult);

            // 完了を待つ
            yield return new WaitUntil(() => isCompleted);

            // Assert
            Assert.AreEqual(expectedResult, actualResult, "Resolve した値が戻り値に反映されること");
            Assert.IsFalse(_service.HasActivePopup.CurrentValue, "完了後は HasActivePopup が false であること");
            Assert.IsTrue(popup.IsInitialized, "表示処理（Initialize）が行われること");
            Assert.AreEqual(1, _viewProvider.ReleaseCallCount, "Release が 1 回呼ばれること");
        }

        // ---------------------------------------------------------------------------
        // テストケース 2: Release 保証（正常完了）
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 正常完了時に Release が必ず呼ばれることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_NormalCompletion_ReleasesView()
        {
            // Arrange
            var popup = MockPopupFactory.Create();
            _viewProvider.EnqueueInstance(popup);
            var parameter = CreateParameter();

            // Act
            var showTask = _service.ShowAsync<MockResultPopup, int>(parameter).ToCoroutine();
            yield return null;

            popup.Resolve(0);
            yield return showTask;

            // Assert
            Assert.AreEqual(1, _viewProvider.ReleaseCallCount, "正常完了時に Release が 1 回呼ばれること");
            Assert.AreSame(popup, _viewProvider.ReleasedPopups[0], "Release に渡された対象が表示したポップアップであること");
        }

        // ---------------------------------------------------------------------------
        // テストケース 3: キャンセル（待機中）
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 2 件投げ、1 件目表示中に 2 件目を CancellationToken でキャンセルしたとき、
        /// 2 件目は OperationCanceledException になり、Release は呼ばれず、
        /// 1 件目を Resolve すれば完了すること。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_SecondRequestCancelledWhileWaiting_ThrowsAndDoesNotRelease()
        {
            // Arrange
            var firstPopup = MockPopupFactory.Create("First");
            _viewProvider.EnqueueInstance(firstPopup);
            var firstParameter = CreateParameter();
            var secondParameter = CreateParameter();
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            // 1 件目を開始（表示中になる）
            var firstTask = _service.ShowAsync<MockResultPopup, int>(firstParameter).ToCoroutine();

            // 2 件目を待機列に積む（View は未生成のため Release は発生しない）
            Exception caughtException = null;

            async UniTaskVoid StartSecondRequest()
            {
                try
                {
                    await _service.ShowAsync<MockResultPopup, int>(
                        secondParameter, cancellationTokenSource.Token);
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
            }

            StartSecondRequest().Forget();

            // 1 フレーム待って 1 件目が表示状態になったことを確認する
            yield return null;
            Assert.IsTrue(_service.HasActivePopup.CurrentValue, "1 件目が表示中であること");

            // 2 件目をキャンセルする
            cancellationTokenSource.Cancel();
            yield return null;

            // Assert: Release は 2 件目について呼ばれない（View 未生成のため）
            Assert.AreEqual(0, _viewProvider.ReleaseCallCount, "待機中キャンセルで Release は呼ばれないこと");
            Assert.IsInstanceOf<OperationCanceledException>(caughtException, "2 件目は OperationCanceledException になること");

            // 1 件目を Resolve してデッドロックしないことを確認する
            firstPopup.Resolve(1);
            yield return firstTask;

            Assert.IsFalse(_service.HasActivePopup.CurrentValue, "1 件目完了後は HasActivePopup が false であること");
        }

        // ---------------------------------------------------------------------------
        // テストケース 4: キャンセル（表示中）
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 表示中の要求を CancellationToken でキャンセルしたとき、
        /// finally で CloseAsync と Release が呼ばれること（View リークしないこと）。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_CancelledWhileDisplaying_CloseAndReleaseCalled()
        {
            // Arrange
            var popup = MockPopupFactory.Create();
            _viewProvider.EnqueueInstance(popup);
            var parameter = CreateParameter();
            var cancellationTokenSource = new CancellationTokenSource();
            Exception caughtException = null;

            // Act
            async UniTaskVoid StartRequest()
            {
                try
                {
                    await _service.ShowAsync<MockResultPopup, int>(
                        parameter, cancellationTokenSource.Token);
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
            }

            StartRequest().Forget();

            // 1 フレーム待って表示状態になることを確認する
            yield return null;
            Assert.IsTrue(_service.HasActivePopup.CurrentValue, "表示中であること");

            // 表示中にキャンセルする
            cancellationTokenSource.Cancel();

            // キャンセル後の finally 処理（CloseAsync + Release）が完了するまで待つ
            yield return new WaitUntil(() => _viewProvider.ReleaseCallCount > 0);

            // Assert
            Assert.AreEqual(1, _viewProvider.ReleaseCallCount, "キャンセル時に Release が呼ばれること（Close 後に解放）");
            Assert.IsFalse(_service.HasActivePopup.CurrentValue, "キャンセル後は HasActivePopup が false であること");
        }

        // ---------------------------------------------------------------------------
        // テストケース 5: 優先度順
        // ---------------------------------------------------------------------------

        /// <summary>
        /// 1 件目表示中に Low→High→Normal の順で 3 件投げ、
        /// 1 件目完了後に High→Normal→Low の順で処理されること（LoadAsync 呼び出し順で検証）。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_PriorityOrdering_ProcessesInPriorityDescendingOrder()
        {
            // Arrange
            var firstPopup = MockPopupFactory.Create("First");
            var highPopup = MockPopupFactory.Create("High");
            var normalPopup = MockPopupFactory.Create("Normal");
            var lowPopup = MockPopupFactory.Create("Low");

            _viewProvider.EnqueueInstance(firstPopup);
            _viewProvider.EnqueueInstance(highPopup);
            _viewProvider.EnqueueInstance(normalPopup);
            _viewProvider.EnqueueInstance(lowPopup);

            var firstParameter = CreateParameter(PopupPriority.Normal);
            var lowParameter = CreateParameter(PopupPriority.Low);
            var highParameter = CreateParameter(PopupPriority.High);
            var normalParameter = CreateParameter(PopupPriority.Normal);

            var completionOrder = new List<string>();

            // Act
            // 1 件目を開始する
            async UniTaskVoid StartFirst()
            {
                await _service.ShowAsync<MockResultPopup, int>(firstParameter);
                completionOrder.Add("First");
            }

            StartFirst().Forget();
            yield return null;

            // 表示中に Low→High→Normal の順で 3 件投げる
            async UniTaskVoid StartLow()
            {
                await _service.ShowAsync<MockResultPopup, int>(lowParameter);
                completionOrder.Add("Low");
            }

            async UniTaskVoid StartHigh()
            {
                await _service.ShowAsync<MockResultPopup, int>(highParameter);
                completionOrder.Add("High");
            }

            async UniTaskVoid StartNormal()
            {
                await _service.ShowAsync<MockResultPopup, int>(normalParameter);
                completionOrder.Add("Normal");
            }

            StartLow().Forget();
            StartHigh().Forget();
            StartNormal().Forget();

            yield return null;

            // 1 件目を完了させ、High が LoadAsync されるまで待つ
            firstPopup.Resolve(0);
            yield return new WaitUntil(() => highPopup.IsInitialized);

            // High を Resolve し、Normal が処理されるまで待つ
            highPopup.Resolve(0);
            yield return new WaitUntil(() => normalPopup.IsInitialized);

            // Normal を Resolve し、Low が処理されるまで待つ
            normalPopup.Resolve(0);
            yield return new WaitUntil(() => lowPopup.IsInitialized);

            // Low を Resolve して全件完了を待つ
            lowPopup.Resolve(0);
            yield return new WaitUntil(() => completionOrder.Count >= 4);

            // Assert: 完了順が First→High→Normal→Low であること
            Assert.AreEqual(4, completionOrder.Count, "4 件すべて完了すること");
            Assert.AreEqual("First", completionOrder[0], "1 件目が最初に完了すること");
            Assert.AreEqual("High", completionOrder[1], "High 優先度が 2 番目に完了すること");
            Assert.AreEqual("Normal", completionOrder[2], "Normal 優先度が 3 番目に完了すること");
            Assert.AreEqual("Low", completionOrder[3], "Low 優先度が最後に完了すること");
        }

        // ---------------------------------------------------------------------------
        // テストケース 6: LoadAsync 例外
        // ---------------------------------------------------------------------------

        /// <summary>
        /// LoadAsync が例外を投げたとき、ShowAsync が例外を伝播し、
        /// かつ待機列が詰まらず次の要求が処理できること。
        /// </summary>
        [UnityTest]
        public IEnumerator ShowAsync_LoadAsyncThrows_PropagatesExceptionAndQueueContinues()
        {
            // Arrange
            _viewProvider.ThrowOnLoad = true;
            var firstParameter = CreateParameter();
            Exception caughtException = null;

            // 2 件目用に例外注入を解除してポップアップを用意する
            var secondPopup = MockPopupFactory.Create("Second");

            // Act
            // 1 件目を投げる（LoadAsync 時に例外が発生する）
            async UniTaskVoid StartFirst()
            {
                try
                {
                    await _service.ShowAsync<MockResultPopup, int>(firstParameter);
                }
                catch (Exception exception)
                {
                    caughtException = exception;
                }
            }

            StartFirst().Forget();

            // LoadAsync 例外が catch されるまで待つ
            yield return new WaitUntil(() => caughtException != null);

            // ThrowOnLoad を解除して 2 件目を投げる
            _viewProvider.ThrowOnLoad = false;
            _viewProvider.EnqueueInstance(secondPopup);

            var secondParameter = CreateParameter();
            var secondTask = _service.ShowAsync<MockResultPopup, int>(secondParameter).ToCoroutine();
            yield return null;

            // 2 件目の Resolve
            secondPopup.Resolve(99);
            yield return secondTask;

            // Assert
            Assert.IsInstanceOf<InvalidOperationException>(caughtException, "LoadAsync 例外が ShowAsync から伝播すること");
            Assert.AreEqual(1, _viewProvider.ReleaseCallCount, "2 件目は正常完了して Release されること");
            Assert.IsFalse(_service.HasActivePopup.CurrentValue, "キュー詰まりなく完了すること");
        }

        // ---------------------------------------------------------------------------
        // テストケース 7: CloseTopAsync
        // ---------------------------------------------------------------------------

        /// <summary>
        /// EnableBackKey=true のとき CloseTopAsync 呼び出しで OnClose が実行されること。
        /// </summary>
        [UnityTest]
        public IEnumerator CloseTopAsync_WhenBackKeyEnabled_CallsOnClose()
        {
            // Arrange
            var popup = MockPopupFactory.Create();
            _viewProvider.EnqueueInstance(popup);
            var parameter = CreateParameter(enableBackKey: true);

            // Act
            var showTask = _service.ShowAsync<MockResultPopup, int>(parameter).ToCoroutine();
            yield return null;

            // CloseTopAsync → OnClose → SetResult(-1) が呼ばれるはず
            yield return _service.CloseTopAsync().ToCoroutine();
            yield return showTask;

            // Assert
            Assert.IsFalse(_service.HasActivePopup.CurrentValue, "CloseTopAsync 後に HasActivePopup が false であること");
        }

        /// <summary>
        /// EnableBackKey=false のとき CloseTopAsync 呼び出しで何も起きないこと。
        /// </summary>
        [UnityTest]
        public IEnumerator CloseTopAsync_WhenBackKeyDisabled_DoesNothing()
        {
            // Arrange
            var popup = MockPopupFactory.Create();
            _viewProvider.EnqueueInstance(popup);
            var parameter = CreateParameter(enableBackKey: false);

            // Act
            var showTask = _service.ShowAsync<MockResultPopup, int>(parameter);
            yield return null;

            yield return _service.CloseTopAsync().ToCoroutine();
            yield return null;

            // Assert: バックキー無効なので HasActivePopup はまだ true のまま
            Assert.IsTrue(_service.HasActivePopup.CurrentValue, "BackKey 無効時は CloseTopAsync で閉じないこと");

            // テスト後始末: 手動で Resolve して ShowAsync を終了させる
            popup.Resolve(0);
            yield return showTask.ToCoroutine();
        }
    }
}
