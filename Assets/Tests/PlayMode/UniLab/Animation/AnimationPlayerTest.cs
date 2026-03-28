using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UniLab.Animation;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniLab.Tests.PlayMode.Animation
{
    public class AnimationPlayerTest
    {
        private GameObject _gameObject;
        private AnimationPlayer _player;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("AnimationPlayerTest");
            // AnimationPlayer requires an Animator component
            _gameObject.AddComponent<Animator>();
            _player = _gameObject.AddComponent<AnimationPlayer>();
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
        public IEnumerator IsPlaying_IsFalse_BeforePlayAsyncCalled()
        {
            yield return null;
            Assert.IsFalse(_player.IsPlaying);
        }

        [UnityTest]
        public IEnumerator PlayAsync_EmptyAnimationName_ReturnImmediately_IsPlayingStaysFalse()
        {
            // PlayAsync with no name set and no override should log a warning and return immediately.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Animation name is null or empty"));

            var completed = false;
            var task = _player.PlayAsync().ContinueWith(() => completed = true);
            task.Forget();

            yield return null;
            yield return null;

            Assert.IsTrue(completed, "PlayAsync should complete immediately when animation name is empty.");
            Assert.IsFalse(_player.IsPlaying);
        }

        [UnityTest]
        public IEnumerator OnPlay_DoesNotFire_WhenAnimationNameEmpty()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Animation name is null or empty"));

            var onPlayFired = false;
            using var subscription = _player.OnPlay.Subscribe(_ => onPlayFired = true);

            _player.PlayAsync().Forget();

            yield return null;
            yield return null;

            Assert.IsFalse(onPlayFired);
        }

        [UnityTest]
        public IEnumerator OnComplete_DoesNotFire_WhenAnimationNameEmpty()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Animation name is null or empty"));

            var onCompleteFired = false;
            using var subscription = _player.OnComplete.Subscribe(_ => onCompleteFired = true);

            _player.PlayAsync().Forget();

            yield return null;
            yield return null;

            Assert.IsFalse(onCompleteFired);
        }

        [UnityTest]
        public IEnumerator Destroy_DisposesObservables_WithoutError()
        {
            // Subscribing before destroy and then destroying should not throw
            using var sub1 = _player.OnPlay.Subscribe(_ => { });
            using var sub2 = _player.OnComplete.Subscribe(_ => { });

            UnityEngine.Object.Destroy(_gameObject);
            _gameObject = null;

            yield return null;
            // No exceptions expected
        }
    }
}
