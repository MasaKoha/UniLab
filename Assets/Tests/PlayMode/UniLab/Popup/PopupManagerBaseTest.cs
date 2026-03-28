using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniLab.Popup;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace UniLab.Tests.PlayMode.Popup
{
    // --- Concrete test doubles ---

    internal class TestPopupParameter : IPopupParameter
    {
        public bool EnableBackKey { get; set; } = true;
        public Func<UniTask> CustomBackAsync { get; set; } = null;
        public bool EnableBackgroundClose { get; set; } = false;
    }

    internal class TestPopup : PopupBase
    {
        public bool OpenAsyncCalled { get; private set; }
        public bool WaitAsyncCalled { get; private set; }
        public bool CloseAsyncCalled { get; private set; }
        public bool OnCloseCalled { get; private set; }

        // Completion source used to control when WaitAsync finishes in tests.
        private readonly UniTaskCompletionSource _waitSource = new();

        public void CompleteWait() => _waitSource.TrySetResult();

        protected override void OnInitialize() { }

        public override async UniTask OpenAsync()
        {
            OpenAsyncCalled = true;
            await UniTask.CompletedTask;
        }

        public override async UniTask WaitAsync()
        {
            WaitAsyncCalled = true;
            await _waitSource.Task;
            await CloseAsync();
        }

        public override async UniTask CloseAsync()
        {
            CloseAsyncCalled = true;
            await UniTask.CompletedTask;
        }

        public override void OnClose()
        {
            OnCloseCalled = true;
            _waitSource.TrySetResult();
        }
    }

    internal class TestPopupManager : PopupManagerBase<TestPopupManager>
    {
    }

    // --- Tests ---

    public class PopupManagerBaseTest
    {
        private GameObject _managerGameObject;
        private TestPopupManager _manager;

        [SetUp]
        public void SetUp()
        {
            _managerGameObject = new GameObject("PopupManager");

            // PopupManagerBase requires a _popupRoot Transform via SerializeField.
            // We use the manager's own transform as root for tests.
            _manager = _managerGameObject.AddComponent<TestPopupManager>();
            SetPopupRoot(_manager, _managerGameObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerGameObject != null)
            {
                UnityEngine.Object.Destroy(_managerGameObject);
            }
        }

        // Injects _popupRoot via reflection to avoid requiring a prefab in tests.
        private static void SetPopupRoot(PopupManagerBase<TestPopupManager> manager, Transform root)
        {
            var field = typeof(PopupManagerBase<TestPopupManager>)
                .GetField("_popupRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(manager, root);
        }

        private TestPopup CreateTestPopupPrefab()
        {
            var go = new GameObject("TestPopup");
            // PopupBase requires a Button for _backgroundButton
            var backgroundButtonGo = new GameObject("Background");
            backgroundButtonGo.transform.SetParent(go.transform);
            backgroundButtonGo.AddComponent<Button>();

            var popup = go.AddComponent<TestPopup>();
            var bgField = typeof(PopupBase)
                .GetField("_backgroundButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bgField?.SetValue(popup, backgroundButtonGo.GetComponent<Button>());

            go.SetActive(false);
            return popup;
        }

        [UnityTest]
        public IEnumerator HasActivePopup_IsFalse_Initially()
        {
            yield return null;
            Assert.IsFalse(_manager.HasActivePopup);
        }

        [UnityTest]
        public IEnumerator OpenPopupAsync_SetsHasActivePopupTrue()
        {
            var prefab = CreateTestPopupPrefab();
            var parameter = new TestPopupParameter();
            var instance = _manager.InstantiatePopup(prefab, parameter);

            yield return _manager.OpenPopupAsync(instance).ToCoroutine();

            Assert.IsTrue(_manager.HasActivePopup);
            Assert.IsTrue(instance.OpenAsyncCalled);

            UnityEngine.Object.Destroy(prefab.gameObject);
        }

        [UnityTest]
        public IEnumerator WaitPopupAsync_SetsHasActivePopupFalse_AfterClose()
        {
            var prefab = CreateTestPopupPrefab();
            var parameter = new TestPopupParameter();
            var instance = _manager.InstantiatePopup(prefab, parameter);

            yield return _manager.OpenPopupAsync(instance).ToCoroutine();

            // Complete the wait source so WaitAsync finishes
            instance.CompleteWait();
            yield return _manager.WaitPopupAsync(instance, destroy: false).ToCoroutine();

            Assert.IsFalse(_manager.HasActivePopup);

            UnityEngine.Object.Destroy(prefab.gameObject);
            UnityEngine.Object.Destroy(instance.gameObject);
        }

        [UnityTest]
        public IEnumerator CloseTopPopupAsync_CallsOnClose_WhenBackKeyEnabled()
        {
            var prefab = CreateTestPopupPrefab();
            var parameter = new TestPopupParameter { EnableBackKey = true };
            var instance = _manager.InstantiatePopup(prefab, parameter);

            yield return _manager.OpenPopupAsync(instance).ToCoroutine();
            yield return _manager.CloseTopPopupAsync().ToCoroutine();

            Assert.IsTrue(instance.OnCloseCalled);

            UnityEngine.Object.Destroy(prefab.gameObject);
        }

        [UnityTest]
        public IEnumerator CloseTopPopupAsync_DoesNothing_WhenBackKeyDisabled()
        {
            var prefab = CreateTestPopupPrefab();
            var parameter = new TestPopupParameter { EnableBackKey = false };
            var instance = _manager.InstantiatePopup(prefab, parameter);

            yield return _manager.OpenPopupAsync(instance).ToCoroutine();
            yield return _manager.CloseTopPopupAsync().ToCoroutine();

            Assert.IsFalse(instance.OnCloseCalled);

            UnityEngine.Object.Destroy(prefab.gameObject);
        }
    }
}
