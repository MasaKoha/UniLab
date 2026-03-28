using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UniLab.Scene.Screen;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniLab.Tests.PlayMode.Screen
{
    // --- Concrete test doubles ---

    internal enum TestScreenType
    {
        Home,
        Settings,
        Profile,
    }

    internal class TestScreen : ScreenBase
    {
        public TestScreenType ScreenType;
        public override Enum Type { get => ScreenType; protected set => ScreenType = (TestScreenType)value; }

        protected override void OnInitialize() { }
        protected override UniTask OnPreShowAsync() => UniTask.CompletedTask;
        protected override void OnShow() { }
        protected override UniTask OnPreHideAsync() => UniTask.CompletedTask;
        protected override void OnHide() { }
        public override void Dispose() { }
    }

    internal class TestScreenManager : ScreenManagerBase<TestScreen>
    {
    }

    // --- Tests ---

    public class ScreenManagerBaseTest
    {
        private GameObject _gameObject;
        private TestScreenManager _manager;
        private List<TestScreen> _screens;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("ScreenManagerTest");
            _manager = _gameObject.AddComponent<TestScreenManager>();

            _screens = new List<TestScreen>();
            foreach (TestScreenType type in Enum.GetValues(typeof(TestScreenType)))
            {
                var go = new GameObject(type.ToString());
                go.transform.SetParent(_gameObject.transform);
                var screen = go.AddComponent<TestScreen>();
                screen.ScreenType = type;
                _screens.Add(screen);
            }

            _manager.RegisterScreens(_screens);
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.Destroy(_gameObject);
            }
        }

        [UnityTest]
        public IEnumerator RegisterScreens_AllScreensStartHidden()
        {
            yield return null;
            foreach (var screen in _screens)
            {
                Assert.IsFalse(screen.gameObject.activeSelf, $"{screen.ScreenType} should start hidden.");
            }
        }

        [UnityTest]
        public IEnumerator ShowAsync_ActivatesTargetScreen()
        {
            yield return _manager.ShowAsync(TestScreenType.Home).ToCoroutine();

            Assert.IsTrue(_screens[0].gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator ShowAsync_HidesPreviousScreen()
        {
            yield return _manager.ShowAsync(TestScreenType.Home).ToCoroutine();
            yield return _manager.ShowAsync(TestScreenType.Settings).ToCoroutine();

            Assert.IsFalse(_screens[0].gameObject.activeSelf, "Home should be hidden.");
            Assert.IsTrue(_screens[1].gameObject.activeSelf, "Settings should be visible.");
        }

        [UnityTest]
        public IEnumerator ShowAsync_SameScreen_IsNoOp()
        {
            yield return _manager.ShowAsync(TestScreenType.Home).ToCoroutine();

            var callCount = 0;
            using var sub = _manager.OnScreenChanged.Subscribe(_ => callCount++);

            yield return _manager.ShowAsync(TestScreenType.Home).ToCoroutine();

            Assert.AreEqual(0, callCount, "OnScreenChanged should not fire for the same screen.");
        }

        [UnityTest]
        public IEnumerator OnScreenChanged_FiresWhenScreenChanges()
        {
            IScreenView lastChanged = null;
            using var sub = _manager.OnScreenChanged.Subscribe(s => lastChanged = s);

            yield return _manager.ShowAsync(TestScreenType.Settings).ToCoroutine();

            Assert.IsNotNull(lastChanged);
            Assert.AreEqual(TestScreenType.Settings, ((TestScreen)lastChanged).ScreenType);
        }

        [UnityTest]
        public IEnumerator BackAsync_DoesNothing_WhenHistoryHasOneEntry()
        {
            yield return _manager.ShowAsync(TestScreenType.Home).ToCoroutine();

            var callCount = 0;
            using var sub = _manager.OnScreenChanged.Subscribe(_ => callCount++);

            yield return _manager.BackAsync().ToCoroutine();

            Assert.AreEqual(0, callCount, "BackAsync should do nothing with only one history entry.");
        }

        [UnityTest]
        public IEnumerator BackAsync_ReturnsToPreivousScreen()
        {
            yield return _manager.ShowAsync(TestScreenType.Home).ToCoroutine();
            yield return _manager.ShowAsync(TestScreenType.Settings).ToCoroutine();
            yield return _manager.BackAsync().ToCoroutine();

            Assert.IsTrue(_screens[0].gameObject.activeSelf, "Home should be active after Back.");
            Assert.IsFalse(_screens[1].gameObject.activeSelf, "Settings should be hidden after Back.");
        }

        [UnityTest]
        public IEnumerator BackAsync_TwiceAfterTwoShows_LandsOnFirst()
        {
            yield return _manager.ShowAsync(TestScreenType.Home).ToCoroutine();
            yield return _manager.ShowAsync(TestScreenType.Settings).ToCoroutine();
            yield return _manager.ShowAsync(TestScreenType.Profile).ToCoroutine();
            yield return _manager.BackAsync().ToCoroutine();
            yield return _manager.BackAsync().ToCoroutine();

            Assert.IsTrue(_screens[0].gameObject.activeSelf, "Should be back on Home.");
        }
    }
}
