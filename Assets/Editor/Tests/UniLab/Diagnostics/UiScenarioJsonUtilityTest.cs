using NUnit.Framework;
using UniLab.Diagnostics;
using UnityEngine;

namespace UniLab.Tests.EditMode.Diagnostics
{
    public class UiScenarioJsonUtilityTest
    {
        [Test]
        public void JsonUtility_RoundTripsScenario()
        {
            var originalScenario = new UiScenario
            {
                outputDirectory = "DebugOutput/ui-scenario",
                steps = new[]
                {
                    new UiScenarioStep
                    {
                        waitScene = "Title",
                        capture = "01_title",
                        settleFrames = 30,
                    },
                    new UiScenarioStep
                    {
                        submit = "MenuRoot/PlayButton",
                        audit = true,
                        settleFrames = 60,
                    },
                },
            };

            var json = JsonUtility.ToJson(originalScenario);
            var restoredScenario = JsonUtility.FromJson<UiScenario>(json);

            Assert.That(restoredScenario, Is.Not.Null);
            Assert.That(restoredScenario.outputDirectory, Is.EqualTo(originalScenario.outputDirectory));
            Assert.That(restoredScenario.steps, Is.Not.Null);
            Assert.That(restoredScenario.steps.Length, Is.EqualTo(2));
            Assert.That(restoredScenario.steps[0].waitScene, Is.EqualTo("Title"));
            Assert.That(restoredScenario.steps[0].capture, Is.EqualTo("01_title"));
            Assert.That(restoredScenario.steps[0].settleFrames, Is.EqualTo(30));
            Assert.That(restoredScenario.steps[1].submit, Is.EqualTo("MenuRoot/PlayButton"));
            Assert.That(restoredScenario.steps[1].audit, Is.True);
            Assert.That(restoredScenario.steps[1].settleFrames, Is.EqualTo(60));
        }
    }
}
