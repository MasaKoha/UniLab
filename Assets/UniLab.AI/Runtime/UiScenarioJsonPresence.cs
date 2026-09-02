#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text.RegularExpressions;

namespace UniLab.AI
{
    /// <summary>
    /// JsonUtility が未指定 bool と false 指定を区別できないため、ランナーに必要な存在情報だけを補います。
    /// </summary>
    public static class UiScenarioJsonPresence
    {
        private const string InputOverlayPattern = "\"inputOverlay\"\\s*:";
        private const string InputOverlayTruePattern = "\"inputOverlay\"\\s*:\\s*true";
        private const string InputOverlayFalsePattern = "\"inputOverlay\"\\s*:\\s*false";
        private const string StepsPattern = "\"steps\"\\s*:";
        private const string MonkeyPattern = "\"monkey\"\\s*:";

        /// <summary>
        /// シナリオ直下の `inputOverlay` 指定有無を反映し、録画既定表示との違いを維持します。
        /// </summary>
        public static void Apply(string scenarioJson, UiScenario scenario)
        {
            if (scenario == null)
            {
                return;
            }

            var json = scenarioJson ?? string.Empty;
            scenario.inputOverlaySpecified = IsTopLevelInputOverlaySpecified(json);
            ApplyStepInputOverlay(json, scenario);
        }

        private static bool IsTopLevelInputOverlaySpecified(string json)
        {
            var stepsMatch = Regex.Match(json, StepsPattern);
            var topLevelText = stepsMatch.Success ? json.Substring(0, stepsMatch.Index) : json;
            return Regex.IsMatch(topLevelText, InputOverlayPattern);
        }

        private static void ApplyStepInputOverlay(string json, UiScenario scenario)
        {
            if (scenario.steps == null || scenario.steps.Length == 0)
            {
                return;
            }

            var stepsMatch = Regex.Match(json, StepsPattern);
            if (!stepsMatch.Success)
            {
                return;
            }

            var arrayStart = json.IndexOf('[', stepsMatch.Index);
            if (arrayStart < 0)
            {
                return;
            }

            var stepIndex = 0;
            var depth = 0;
            var objectStart = -1;
            var insideString = false;
            for (var characterIndex = arrayStart + 1; characterIndex < json.Length && stepIndex < scenario.steps.Length; characterIndex++)
            {
                var character = json[characterIndex];
                if (character == '"' && !IsEscaped(json, characterIndex))
                {
                    insideString = !insideString;
                    continue;
                }

                if (insideString)
                {
                    continue;
                }

                if (character == '{')
                {
                    if (depth == 0)
                    {
                        objectStart = characterIndex;
                    }

                    depth++;
                    continue;
                }

                if (character != '}')
                {
                    continue;
                }

                depth--;
                if (depth == 0 && objectStart >= 0)
                {
                    ApplyStepObject(json.Substring(objectStart, characterIndex - objectStart + 1), scenario.steps[stepIndex]);
                    stepIndex++;
                    objectStart = -1;
                }
            }
        }

        private static void ApplyStepObject(string stepJson, UiScenarioStep step)
        {
            if (step == null)
            {
                return;
            }

            // JsonUtility は JSON に無いネストオブジェクトも既定インスタンスで埋めるため、
            // "monkey" キーが無いステップは null に戻す（全ステップがモンキーテスト扱いになる事故の防止）
            if (!Regex.IsMatch(stepJson, MonkeyPattern))
            {
                step.monkey = null;
            }

            step.inputOverlaySpecified = Regex.IsMatch(stepJson, InputOverlayPattern);
            if (Regex.IsMatch(stepJson, InputOverlayTruePattern))
            {
                step.inputOverlay = true;
                return;
            }

            if (Regex.IsMatch(stepJson, InputOverlayFalsePattern))
            {
                step.inputOverlay = false;
            }
        }

        private static bool IsEscaped(string text, int quoteIndex)
        {
            var slashCount = 0;
            for (var characterIndex = quoteIndex - 1; characterIndex >= 0 && text[characterIndex] == '\\'; characterIndex--)
            {
                slashCount++;
            }

            return slashCount % 2 == 1;
        }
    }
}
#endif
