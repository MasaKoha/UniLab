#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UniLab.AI
{
    /// <summary>
    /// 画面上の意味ある UI 状態を構造化データへ落とし込みます。
    /// AI が画像を見ずに判断できる入口を統一するためです。
    /// </summary>
    public static class UiSnapshot
    {
        private const string SnapshotDirectoryName = "snapshots";
        private const string FileNamePrefix = "snapshot-";
        private const string FileNameTimestampFormat = "yyyyMMdd-HHmmss-fff";
        private const string FileExtension = ".json";
        private const string ButtonKind = "Button";
        private const string ToggleKind = "Toggle";
        private const string SliderKind = "Slider";
        private const string InputKind = "Input";
        private const string SelectableKind = "Selectable";
        private const string TextKind = "Text";
        private const int SelectableLabelLength = 80;
        private const float MinimumScreenVisibleRatio = 0.1f;
        private const float MinimumMaskVisibleRatio = 0.5f;
        private const int TextLabelLength = 120;
        private const int CompactTextCollapseThreshold = 5;
        private const int CompactTextExpandedHeadCount = 3;

        /// <summary>
        /// 現在フレーム内で完結する UI 状態を収集します。
        /// フレームをまたがる待機を入れないことで観測がゲームの見え方を変えないようにします。
        /// </summary>
        public static UiSnapshotDocument Capture()
        {
            var selectedObject = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            var elements = CollectElements(selectedObject);
            var gameEntries = CollectGameEntries();

            return new UiSnapshotDocument
            {
                capturedAt = DateTimeOffset.Now.ToString("o"),
                frame = Time.frameCount,
                activeScene = SceneManager.GetActiveScene().name,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                focusedPath = selectedObject == null ? string.Empty : UiVisibilityUtility.BuildPath(selectedObject.transform),
                elements = elements.ToArray(),
                game = gameEntries.ToArray(),
            };
        }

        /// <summary>
        /// スナップショットを JSON へ保存します。
        /// 人が見つけやすい既定出力先へ寄せて、他ツールと成果物の置き場を揃えます。
        /// </summary>
        public static string Save(UiSnapshotDocument document, string outputDirectory = null)
        {
            var resolvedOutputDirectory = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(DebugOutputPath.DirectoryPath, SnapshotDirectoryName)
                : outputDirectory;
            Directory.CreateDirectory(resolvedOutputDirectory);

            var filePath = Path.Combine(
                resolvedOutputDirectory,
                $"{FileNamePrefix}{DateTime.Now.ToString(FileNameTimestampFormat)}{FileExtension}");
            var json = JsonUtility.ToJson(document, true);
            File.WriteAllText(filePath, json);
            return filePath;
        }

        /// <summary>
        /// シナリオの `snapshot` 名と成果物名を一致させ、後続ツールがステップ名で証拠へ到達できるようにします。
        /// </summary>
        public static string Save(UiSnapshotDocument document, string outputDirectory, string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension))
            {
                return Save(document, outputDirectory);
            }

            var resolvedOutputDirectory = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(DebugOutputPath.DirectoryPath, SnapshotDirectoryName)
                : outputDirectory;
            Directory.CreateDirectory(resolvedOutputDirectory);
            var filePath = Path.Combine(resolvedOutputDirectory, $"{fileNameWithoutExtension}{FileExtension}");
            File.WriteAllText(filePath, JsonUtility.ToJson(document, true));
            return filePath;
        }

        /// <summary>
        /// スナップショットを AI 向けの圧縮テキストへ変換します。
        /// 座標や内部詳細を省いてトークン効率を上げるためです。
        /// </summary>
        public static string ToCompactText(UiSnapshotDocument document, string scope = null)
        {
            if (document == null)
            {
                return string.Empty;
            }

            document = UiObservationScope.Filter(document, scope);
            var lineBuilder = new StringBuilder();
            lineBuilder.Append("scene=");
            lineBuilder.Append(string.IsNullOrEmpty(document.activeScene) ? "-" : document.activeScene);
            lineBuilder.Append(" focus=");
            AppendFocusSummary(lineBuilder, document);
            lineBuilder.AppendLine();

            AppendCompactElements(lineBuilder, document.elements);

            if (document.game != null && document.game.Length > 0)
            {
                lineBuilder.Append("game:");
                for (var gameIndex = 0; gameIndex < document.game.Length; gameIndex++)
                {
                    var gameEntry = document.game[gameIndex];
                    if (gameEntry == null)
                    {
                        continue;
                    }

                    lineBuilder.Append(" ");
                    lineBuilder.Append(gameEntry.key);
                    lineBuilder.Append("=");
                    lineBuilder.Append(gameEntry.value);
                }
            }

            return lineBuilder.ToString().TrimEnd();
        }

        /// <summary>
        /// 2 つのスナップショット差分を返します。
        /// 操作結果が空振りかどうかを要素単位で即判定できるようにします。
        /// </summary>
        public static UiSnapshotDiff Compare(UiSnapshotDocument before, UiSnapshotDocument after)
        {
            var beforeMap = BuildElementMap(before == null ? null : before.elements);
            var afterMap = BuildElementMap(after == null ? null : after.elements);
            var addedPaths = new List<string>();
            var removedPaths = new List<string>();
            var changedEntries = new List<UiSnapshotChange>();

            foreach (var beforePair in beforeMap)
            {
                if (!afterMap.ContainsKey(beforePair.Key))
                {
                    removedPaths.Add(beforePair.Key);
                }
            }

            foreach (var afterPair in afterMap)
            {
                if (!beforeMap.TryGetValue(afterPair.Key, out var beforeElement))
                {
                    addedPaths.Add(afterPair.Key);
                    continue;
                }

                AppendChangedField(changedEntries, afterPair.Key, "name", beforeElement.name, afterPair.Value.name);
                AppendChangedField(changedEntries, afterPair.Key, "kind", beforeElement.kind, afterPair.Value.kind);
                AppendChangedField(changedEntries, afterPair.Key, "label", beforeElement.label, afterPair.Value.label);
                AppendChangedField(changedEntries, afterPair.Key, "rect", FormatRect(beforeElement.rect), FormatRect(afterPair.Value.rect));
                AppendChangedField(changedEntries, afterPair.Key, "interactable", FormatBoolean(beforeElement.interactable), FormatBoolean(afterPair.Value.interactable));
                AppendChangedField(changedEntries, afterPair.Key, "blockedBy", beforeElement.blockedBy, afterPair.Value.blockedBy);
                AppendChangedField(changedEntries, afterPair.Key, "focused", FormatBoolean(beforeElement.focused), FormatBoolean(afterPair.Value.focused));
                AppendChangedField(changedEntries, afterPair.Key, "value", beforeElement.value, afterPair.Value.value);
                AppendChangedField(changedEntries, afterPair.Key, "clipped", FormatBoolean(beforeElement.clipped), FormatBoolean(afterPair.Value.clipped));
                AppendChangedField(changedEntries, afterPair.Key, "offscreen", FormatBoolean(beforeElement.offscreen), FormatBoolean(afterPair.Value.offscreen));
            }

            addedPaths.Sort(StringComparer.Ordinal);
            removedPaths.Sort(StringComparer.Ordinal);
            changedEntries.Sort((left, right) =>
            {
                var pathComparison = string.Compare(left.path, right.path, StringComparison.Ordinal);
                if (pathComparison != 0)
                {
                    return pathComparison;
                }

                return string.Compare(left.field, right.field, StringComparison.Ordinal);
            });

            var diff = new UiSnapshotDiff
            {
                addedPaths = addedPaths.ToArray(),
                removedPaths = removedPaths.ToArray(),
                changed = changedEntries.ToArray(),
                focusedBefore = before == null ? string.Empty : before.focusedPath,
                focusedAfter = after == null ? string.Empty : after.focusedPath,
                sceneBefore = before == null ? string.Empty : before.activeScene,
                sceneAfter = after == null ? string.Empty : after.activeScene,
            };
            diff.isEmpty =
                diff.addedPaths.Length == 0 &&
                diff.removedPaths.Length == 0 &&
                diff.changed.Length == 0 &&
                string.Equals(diff.focusedBefore, diff.focusedAfter, StringComparison.Ordinal) &&
                string.Equals(diff.sceneBefore, diff.sceneAfter, StringComparison.Ordinal);
            return diff;
        }

        private static List<UiSnapshotElement> CollectElements(GameObject selectedObject)
        {
            var elements = new List<UiSnapshotElement>();
            var selectableObjects = UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None);
            for (var selectableIndex = 0; selectableIndex < selectableObjects.Length; selectableIndex++)
            {
                var selectable = selectableObjects[selectableIndex];
                if (selectable == null || !UiVisibilityUtility.IsVisibleGraphicObject(selectable.gameObject))
                {
                    continue;
                }

                if (!TryCreateSelectableElement(selectable, selectedObject, out var element))
                {
                    continue;
                }

                elements.Add(element);
            }

            var textObjects = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            for (var textIndex = 0; textIndex < textObjects.Length; textIndex++)
            {
                var textObject = textObjects[textIndex];
                if (textObject == null || !UiVisibilityUtility.IsVisibleGraphicObject(textObject.gameObject))
                {
                    continue;
                }

                if (textObject.GetComponentInParent<Selectable>() != null)
                {
                    continue;
                }

                if (!TryCreateTextElement(textObject, selectedObject, out var element))
                {
                    continue;
                }

                elements.Add(element);
            }

            elements.Sort(CompareElements);
            return elements;
        }

        private static bool TryCreateSelectableElement(Selectable selectable, GameObject selectedObject, out UiSnapshotElement element)
        {
            element = null;
            var rectTransform = selectable.transform as RectTransform;
            if (rectTransform == null)
            {
                return false;
            }

            if (!UiVisibilityUtility.TryGetScreenRect(rectTransform, out var rectValues))
            {
                return false;
            }

            var inputField = selectable as TMP_InputField;
            var toggle = selectable as Toggle;
            var slider = selectable as Slider;
            var button = selectable as Button;
            var label = inputField == null
                ? UiVisibilityUtility.FindSelectableLabel(selectable.gameObject, SelectableLabelLength)
                : GetInputPlaceholderLabel(inputField);

            element = new UiSnapshotElement
            {
                path = UiVisibilityUtility.BuildPath(selectable.transform),
                name = selectable.name,
                kind = ResolveSelectableKind(button, toggle, slider, inputField),
                label = label,
                rect = rectValues,
                offscreen = UiVisibilityUtility.ComputeVisibleRatio(rectValues, new[] { 0f, 0f, (float)Screen.width, Screen.height }) < MinimumScreenVisibleRatio,
                clipped = IsClipped(rectTransform, rectValues),
                interactable = UiVisibilityUtility.IsInteractable(selectable),
                blockedBy = GetBlockingObjectName(selectable.gameObject),
                focused = selectedObject == selectable.gameObject,
                value = ResolveSelectableValue(toggle, slider, inputField),
            };
            return true;
        }

        private static bool TryCreateTextElement(TextMeshProUGUI textObject, GameObject selectedObject, out UiSnapshotElement element)
        {
            element = null;
            var rectTransform = textObject.transform as RectTransform;
            if (rectTransform == null)
            {
                return false;
            }

            if (!UiVisibilityUtility.TryGetScreenRect(rectTransform, out var rectValues))
            {
                return false;
            }

            element = new UiSnapshotElement
            {
                path = UiVisibilityUtility.BuildPath(textObject.transform),
                name = textObject.name,
                kind = TextKind,
                label = UiVisibilityUtility.Truncate(textObject.text, TextLabelLength),
                rect = rectValues,
                offscreen = UiVisibilityUtility.ComputeVisibleRatio(rectValues, new[] { 0f, 0f, (float)Screen.width, Screen.height }) < MinimumScreenVisibleRatio,
                clipped = IsClipped(rectTransform, rectValues),
                interactable = false,
                blockedBy = string.Empty,
                focused = selectedObject == textObject.gameObject,
                value = string.Empty,
            };
            return true;
        }

        private static bool IsClipped(RectTransform elementTransform, float[] elementRect)
        {
            // perf: 観測時のみ祖先を探索し、毎フレームの階層検索を避ける。
            for (var ancestor = elementTransform.parent; ancestor != null; ancestor = ancestor.parent)
            {
                var rectangleMask = ancestor.GetComponent<RectMask2D>();
                var mask = ancestor.GetComponent<Mask>();
                var hasRectangleMask = rectangleMask != null && rectangleMask.isActiveAndEnabled;
                var hasImageMask = mask != null && mask.isActiveAndEnabled && ancestor.GetComponent<Image>() != null;
                if (!hasRectangleMask && !hasImageMask)
                {
                    continue;
                }

                return UiVisibilityUtility.TryGetScreenRect(ancestor as RectTransform, out var clipRect)
                    && UiVisibilityUtility.ComputeVisibleRatio(elementRect, clipRect) < MinimumMaskVisibleRatio;
            }

            return false;
        }

        private static List<UiSnapshotGameEntry> CollectGameEntries()
        {
            var entries = new List<UiSnapshotGameEntry>();
            var stateProvider = GameAdapterRegistry.StateProvider;
            if (stateProvider == null)
            {
                return entries;
            }

            IReadOnlyDictionary<string, object> state;
            try
            {
                state = stateProvider.GetState();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[UiSnapshot] ゲーム状態の収集に失敗しました。 {exception.GetType().Name}: {exception.Message}");
                return entries;
            }

            if (state == null)
            {
                return entries;
            }

            var keys = new List<string>(state.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
            {
                var key = keys[keyIndex];
                entries.Add(new UiSnapshotGameEntry
                {
                    key = key ?? string.Empty,
                    value = FormatGameValue(state[key]),
                });
            }

            return entries;
        }

        private static int CompareElements(UiSnapshotElement left, UiSnapshotElement right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var leftY = left.rect == null || left.rect.Length < 2 ? 0f : left.rect[1];
            var rightY = right.rect == null || right.rect.Length < 2 ? 0f : right.rect[1];
            var yComparison = -leftY.CompareTo(rightY);
            if (yComparison != 0)
            {
                return yComparison;
            }

            var leftX = left.rect == null || left.rect.Length < 1 ? 0f : left.rect[0];
            var rightX = right.rect == null || right.rect.Length < 1 ? 0f : right.rect[0];
            var xComparison = leftX.CompareTo(rightX);
            if (xComparison != 0)
            {
                return xComparison;
            }

            return string.Compare(left.path, right.path, StringComparison.Ordinal);
        }

        private static string ResolveSelectableKind(Button button, Toggle toggle, Slider slider, TMP_InputField inputField)
        {
            if (button != null)
            {
                return ButtonKind;
            }

            if (toggle != null)
            {
                return ToggleKind;
            }

            if (slider != null)
            {
                return SliderKind;
            }

            if (inputField != null)
            {
                return InputKind;
            }

            return SelectableKind;
        }

        private static string ResolveSelectableValue(Toggle toggle, Slider slider, TMP_InputField inputField)
        {
            if (toggle != null)
            {
                return toggle.isOn ? "on" : "off";
            }

            if (slider != null)
            {
                return slider.value.ToString(CultureInfo.InvariantCulture);
            }

            if (inputField != null)
            {
                return inputField.text ?? string.Empty;
            }

            return string.Empty;
        }

        private static string GetInputPlaceholderLabel(TMP_InputField inputField)
        {
            if (inputField == null || inputField.placeholder == null)
            {
                return string.Empty;
            }

            var textObject = inputField.placeholder.GetComponent<TextMeshProUGUI>();
            if (textObject == null)
            {
                return string.Empty;
            }

            return UiVisibilityUtility.Truncate(textObject.text, SelectableLabelLength);
        }

        private static string GetBlockingObjectName(GameObject target)
        {
            var blockingObject = UiVisibilityUtility.FindBlockingObject(target);
            return blockingObject == null ? string.Empty : blockingObject.name;
        }

        private static string FormatGameValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            switch (value)
            {
                case string stringValue:
                    return stringValue;
                case bool boolValue:
                    return boolValue ? "true" : "false";
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }

        private static string FormatBoolean(bool value)
        {
            return value ? "true" : "false";
        }

        private static Dictionary<string, UiSnapshotElement> BuildElementMap(UiSnapshotElement[] elements)
        {
            var map = new Dictionary<string, UiSnapshotElement>(StringComparer.Ordinal);
            if (elements == null)
            {
                return map;
            }

            for (var elementIndex = 0; elementIndex < elements.Length; elementIndex++)
            {
                var element = elements[elementIndex];
                if (element == null || string.IsNullOrEmpty(element.path))
                {
                    continue;
                }

                map[element.path] = element;
            }

            return map;
        }

        private static void AppendChangedField(List<UiSnapshotChange> changedEntries, string path, string fieldName, string beforeValue, string afterValue)
        {
            if (string.Equals(beforeValue ?? string.Empty, afterValue ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            changedEntries.Add(new UiSnapshotChange
            {
                path = path ?? string.Empty,
                field = fieldName ?? string.Empty,
                before = beforeValue ?? string.Empty,
                after = afterValue ?? string.Empty,
            });
        }

        private static string FormatRect(float[] rectValues)
        {
            if (rectValues == null || rectValues.Length < 4)
            {
                return string.Empty;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.###},{1:0.###},{2:0.###},{3:0.###}",
                rectValues[0],
                rectValues[1],
                rectValues[2],
                rectValues[3]);
        }

        private static void AppendFocusSummary(StringBuilder lineBuilder, UiSnapshotDocument document)
        {
            if (string.IsNullOrEmpty(document.focusedPath) || document.elements == null)
            {
                lineBuilder.Append("-");
                return;
            }

            for (var elementIndex = 0; elementIndex < document.elements.Length; elementIndex++)
            {
                var element = document.elements[elementIndex];
                if (element == null || !string.Equals(element.path, document.focusedPath, StringComparison.Ordinal))
                {
                    continue;
                }

                lineBuilder.Append(GetCompactElementName(element));
                if (!string.IsNullOrEmpty(element.label))
                {
                    lineBuilder.Append("(");
                    lineBuilder.Append(element.label);
                    lineBuilder.Append(")");
                }

                return;
            }

            lineBuilder.Append(document.focusedPath);
        }

        private static void AppendCompactElements(StringBuilder lineBuilder, UiSnapshotElement[] elements)
        {
            if (elements == null)
            {
                return;
            }

            for (var elementIndex = 0; elementIndex < elements.Length; elementIndex++)
            {
                var element = elements[elementIndex];
                if (ShouldSkipCompactElement(element))
                {
                    continue;
                }

                var sequenceLength = CountCollapsibleSequenceLength(elements, elementIndex);
                if (sequenceLength < CompactTextCollapseThreshold)
                {
                    AppendCompactElementLine(lineBuilder, element);
                    continue;
                }

                var expandedCount = Math.Min(CompactTextExpandedHeadCount, sequenceLength);
                for (var expandedIndex = 0; expandedIndex < expandedCount; expandedIndex++)
                {
                    AppendCompactElementLine(lineBuilder, elements[elementIndex + expandedIndex]);
                }

                AppendCollapsedSequenceSummary(lineBuilder, element, sequenceLength - expandedCount);
                elementIndex += sequenceLength - 1;
            }
        }

        private static bool ShouldSkipCompactElement(UiSnapshotElement element)
        {
            if (element == null)
            {
                return true;
            }

            return element.kind == TextKind && string.IsNullOrWhiteSpace(element.label);
        }

        private static int CountCollapsibleSequenceLength(UiSnapshotElement[] elements, int startIndex)
        {
            var firstElement = elements[startIndex];
            if (firstElement == null)
            {
                return 0;
            }

            var firstKind = firstElement.kind ?? string.Empty;
            var firstParentPath = GetParentPath(firstElement.path);
            var count = 1;
            for (var elementIndex = startIndex + 1; elementIndex < elements.Length; elementIndex++)
            {
                var element = elements[elementIndex];
                if (ShouldSkipCompactElement(element))
                {
                    break;
                }

                if (firstElement.clipped || element.clipped)
                {
                    break;
                }

                if (!string.Equals(firstKind, element.kind ?? string.Empty, StringComparison.Ordinal))
                {
                    break;
                }

                if (!string.Equals(firstParentPath, GetParentPath(element.path), StringComparison.Ordinal))
                {
                    break;
                }

                count++;
            }

            return count;
        }

        private static void AppendCollapsedSequenceSummary(StringBuilder lineBuilder, UiSnapshotElement element, int collapsedCount)
        {
            if (collapsedCount <= 0)
            {
                return;
            }

            lineBuilder.Append("[");
            lineBuilder.Append(string.IsNullOrEmpty(element.kind) ? "-" : element.kind);
            lineBuilder.Append("] ");
            lineBuilder.Append(GetCompactParentName(element.path));
            lineBuilder.Append(" …他 ");
            lineBuilder.Append(collapsedCount.ToString(CultureInfo.InvariantCulture));
            lineBuilder.AppendLine(" 件");
        }

        private static void AppendCompactElementLine(StringBuilder lineBuilder, UiSnapshotElement element)
        {
            lineBuilder.Append("[");
            lineBuilder.Append(string.IsNullOrEmpty(element.kind) ? "-" : element.kind);
            lineBuilder.Append("] ");
            lineBuilder.Append(GetCompactElementName(element));

            if (!string.IsNullOrEmpty(element.label))
            {
                lineBuilder.Append(" 「");
                lineBuilder.Append(element.label);
                lineBuilder.Append("」");
            }

            if (!element.interactable && element.kind != TextKind)
            {
                lineBuilder.Append(" !disabled");
            }

            if (!string.IsNullOrEmpty(element.blockedBy))
            {
                lineBuilder.Append(" blocked:");
                lineBuilder.Append(element.blockedBy);
            }

            if (element.focused)
            {
                lineBuilder.Append(" *focused");
            }

            if (!string.IsNullOrEmpty(element.value))
            {
                lineBuilder.Append(" value:");
                lineBuilder.Append(element.value);
            }

            if (element.clipped)
            {
                lineBuilder.Append(" [clipped]");
            }

            lineBuilder.AppendLine();
        }

        private static string GetParentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var separatorIndex = path.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                return string.Empty;
            }

            return path.Substring(0, separatorIndex);
        }

        private static string GetCompactElementName(UiSnapshotElement element)
        {
            if (element == null)
            {
                return "-";
            }

            if (string.IsNullOrEmpty(element.path))
            {
                return element.name ?? string.Empty;
            }

            var pathSegments = element.path.Split('/');
            if (pathSegments.Length >= 2)
            {
                return $"{pathSegments[pathSegments.Length - 2]}/{pathSegments[pathSegments.Length - 1]}";
            }

            return element.name ?? element.path;
        }

        private static string GetCompactParentName(string path)
        {
            var parentPath = GetParentPath(path);
            if (string.IsNullOrEmpty(parentPath))
            {
                return "-";
            }

            var pathSegments = parentPath.Split('/');
            if (pathSegments.Length >= 2)
            {
                return $"{pathSegments[pathSegments.Length - 2]}/{pathSegments[pathSegments.Length - 1]}";
            }

            return parentPath;
        }
    }
}
#endif
