#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace UniLab.AI.Tests
{
    /// <summary>シーンを生成せず、スクロール量とシナリオ語彙を検証します。</summary>
    public sealed class UiScrollToTest
    {
        /// <summary>上下左右で共通の最小移動を、はみ出し・内包・巨大要素について検証します。</summary>
        [TestCase(-20f, 20f, 20f)]
        [TestCase(80f, 120f, -20f)]
        [TestCase(20f, 80f, 0f)]
        [TestCase(-20f, 120f, 0f)]
        [TestCase(-120f, 80f, 20f)]
        [TestCase(20f, 220f, -20f)]
        public void ResolveMovementUsesMinimumDisplacement(float targetMinimum, float targetMaximum, float expectedMovement)
        {
            const float ViewportMinimum = 0f;
            const float ViewportMaximum = 100f;
            Assert.That(UiScrollTo.ResolveMovement(targetMinimum, targetMaximum, ViewportMinimum, ViewportMaximum), Is.EqualTo(expectedMovement));
        }

        /// <summary>対象不在の失敗もシナリオの一手として通知し、Input System の有無に依存しません。</summary>
        [Test]
        public void ScenarioReportsMissingScrollTarget()
        {
            const string MissingTarget = "__UniLabScrollToMissingTarget__";
            var failureKind = string.Empty;
            var failureTarget = string.Empty;
            var execution = new ScenarioInputExecutor().ExecuteInputCoroutine(new UiScenarioStep { scrollTo = MissingTarget },
                (kind, target, value, message, evidencePath) =>
                {
                    failureKind = kind;
                    failureTarget = target;
                });
            Assert.That(execution.MoveNext(), Is.False);
            Assert.That(failureKind, Is.EqualTo("scrollTo"));
            Assert.That(failureTarget, Is.EqualTo(MissingTarget));
        }

        /// <summary>JSON 復元後も一手として認識し、対象と種別を保持します。</summary>
        [Test]
        public void ScenarioRecognizesScrollToAction()
        {
            var step = JsonUtility.FromJson<UiScenarioStep>("{\"scrollTo\":\"label:最終行\"}");
            Assert.That(ScenarioInputExecutor.IsInputStep(step), Is.True);
            Assert.That(ScenarioInputExecutor.GetInputKind(step), Is.EqualTo("scrollTo"));
            Assert.That(UiScenarioStepReader.GetPrimaryTarget(step), Is.EqualTo("label:最終行"));
            Assert.That(UiScenarioStepReader.HasAnyAction(step), Is.True);
            Assert.That(step.scroll, Is.Null.Or.Empty);
        }
    }
}
#endif
