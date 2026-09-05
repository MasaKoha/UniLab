#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UniLab.AI.InputOverlayVisualPrimitives;

namespace UniLab.AI
{
    /// <summary>ステップラベル・入力履歴と録画除外マーカーの連携を担います。</summary>
    internal sealed class InputOverlayHistory
    {
        private const int KeyboardChipLimit = 8;
        private const float HistoryItemWidth = 164f;
        private const float HistoryItemHeight = 56f;
        private const float HistoryPanelHeight = 56f;
        private const float HistoryPanelSpacing = 8f;
        private const float HistoryPanelMaxWidthRatio = 0.4f;
        private const float HistoryItemHorizontalPadding = 10f;
        private const float HistoryItemVerticalPadding = 6f;
        private const float HistoryItemMinimumWidth = 56f;
        private const float HistorySeparatorWidth = 16f;
        private static readonly Color HistoryPanelColor = new Color(0f, 0f, 0f, 0f);
        private static readonly Color HistoryTextColor = ParseHtmlColor("#F5F0E6");
        private static readonly Color HistoryTimeColor = new Color(0.9607843f, 0.9411765f, 0.9019608f, 0.62f);
        private static readonly Color HistoryItemBackgroundColor = ParseHtmlColor("#00000099");
        private const float GamepadPanelHeight = 190f;
        private const float KeyboardPanelHeight = 168f;
        private const float DefaultWidgetMargin = 12f;
        private InputOverlayOptions _options;

        /// <summary>録画の除外判定と観測のマーカーを元と同じルートに付けます。</summary>
        internal static void AttachRecordingMarker(GameObject gameObject)
        {
            gameObject.AddComponent<UiOverlayMarker>();
        }

        /// <summary>履歴表示の寿命を機器描画と揃えます。</summary>
        internal void Initialize(RectTransform rootTransform, InputOverlayOptions options)
        {
            _options = options;
            var historyPanelObject = CreatePanel("HistoryPanel", rootTransform, new Vector2(0f, HistoryPanelHeight), HistoryPanelColor);
            _historyPanel = historyPanelObject.rectTransform;
            BuildHistoryContents(_historyPanel);
        }

        /// <summary>再表示時にも同じ設定を履歴へ反映します。</summary>
        internal void ApplyOptions(InputOverlayOptions options)
        {
            _options = options;
            var scale = Mathf.Max(0.1f, _options.scale);
            _historyPanel.localScale = Vector3.one * scale;
            AnchorHistoryPanel(_historyPanel, _options.historyCorner);
        }

        /// <summary>破棄後に履歴の参照を保持しないようにします。</summary>
        internal void Clear()
        {
            _historyItemViews.Clear();
            _historySeparatorViews.Clear();
            _historyEntries.Clear();
        }

        private readonly List<HistoryEntry> _historyEntries = new List<HistoryEntry>();
        private readonly List<HistoryItemView> _historyItemViews = new List<HistoryItemView>();
        private readonly List<TextMeshProUGUI> _historySeparatorViews = new List<TextMeshProUGUI>();
        private RectTransform _historyPanel;

        /// <summary>
        /// 疑似操作のラベルを履歴帯へ追加します。
        /// submit のような実入力を伴わない操作も動画から読み返せるようにするためです。
        /// </summary>
        public void AddSyntheticHistory(string label, float now)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            AddHistoryEntry(label, now);
        }

        /// <summary>録画上で操作と経過時間を読み返せる表示へ更新します。</summary>
        internal void RefreshHistory(float now)
        {
            var visibleCount = Mathf.Min(Mathf.Max(0, _options.historyCount), Mathf.Min(_historyEntries.Count, _historyItemViews.Count));
            _historyPanel.gameObject.SetActive(visibleCount > 0);
            if (visibleCount <= 0)
            {
                HideAllHistoryViews();
                return;
            }

            var historyScale = Mathf.Max(0.1f, _options.scale);
            var maxPanelWidth = (Screen.width * HistoryPanelMaxWidthRatio) / historyScale;
            var visibleHistoryStartIndex = _historyEntries.Count - visibleCount;
            var itemWidths = new float[visibleCount];
            var preferredItemWidthTotal = 0f;
            for (var visibleIndex = 0; visibleIndex < visibleCount; visibleIndex++)
            {
                var itemView = _historyItemViews[visibleIndex];
                var historyEntry = _historyEntries[visibleHistoryStartIndex + visibleIndex];
                EnsureHistoryTextStyle(itemView.label, HistoryTextColor);
                EnsureHistoryTextStyle(itemView.elapsed, HistoryTimeColor);
                itemView.label.text = historyEntry.label;
                itemView.label.ForceMeshUpdate();
                itemView.elapsed.text = historyEntry.cachedElapsedText;
                itemView.elapsed.ForceMeshUpdate();

                var preferredWidth = Mathf.Max(itemView.label.preferredWidth, itemView.elapsed.preferredWidth) + (HistoryItemHorizontalPadding * 2f);
                itemWidths[visibleIndex] = Mathf.Max(HistoryItemMinimumWidth, preferredWidth);
                preferredItemWidthTotal += itemWidths[visibleIndex];
            }

            var separatorCount = Mathf.Max(0, visibleCount - 1);
            var separatorWidthTotal = separatorCount * HistorySeparatorWidth;
            var allowedItemWidthTotal = Mathf.Max(HistoryItemMinimumWidth, maxPanelWidth - separatorWidthTotal);
            if (preferredItemWidthTotal > allowedItemWidthTotal)
            {
                var shrinkRatio = allowedItemWidthTotal / preferredItemWidthTotal;
                for (var visibleIndex = 0; visibleIndex < visibleCount; visibleIndex++)
                {
                    itemWidths[visibleIndex] = Mathf.Max(HistoryItemMinimumWidth, itemWidths[visibleIndex] * shrinkRatio);
                }
            }

            var contentWidth = separatorWidthTotal;
            for (var visibleIndex = 0; visibleIndex < visibleCount; visibleIndex++)
            {
                contentWidth += itemWidths[visibleIndex];
            }

            _historyPanel.sizeDelta = new Vector2(Mathf.Min(maxPanelWidth, contentWidth), HistoryPanelHeight);
            AnchorHistoryPanel(_historyPanel, _options.historyCorner);
            var currentX = 0f;

            for (var viewIndex = 0; viewIndex < _historyItemViews.Count; viewIndex++)
            {
                var itemView = _historyItemViews[viewIndex];
                var historyIndex = visibleHistoryStartIndex + viewIndex;
                var isVisible = viewIndex < visibleCount && historyIndex >= 0;
                itemView.root.gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var entry = _historyEntries[historyIndex];
                itemView.label.text = entry.label;

                var elapsedSeconds = Mathf.Max(0f, now - entry.startedAt);
                var elapsedTenths = Mathf.FloorToInt(elapsedSeconds * 10f);
                if (entry.elapsedTenths != elapsedTenths)
                {
                    // perf: 0.1 秒刻みだけ文字列を更新し、毎フレームの GC を避けます。
                    entry.elapsedTenths = elapsedTenths;
                    entry.cachedElapsedText = $"{elapsedTenths * 0.1f:0.0}s";
                    _historyEntries[historyIndex] = entry;
                }

                itemView.elapsed.text = entry.cachedElapsedText;
                ApplyHistoryItemLayout(itemView, itemWidths[viewIndex], currentX);
                currentX += itemWidths[viewIndex];

                if (viewIndex >= _historySeparatorViews.Count)
                {
                    continue;
                }

                var separator = _historySeparatorViews[viewIndex];
                var shouldShowSeparator = viewIndex < visibleCount - 1;
                separator.gameObject.SetActive(shouldShowSeparator);
                if (shouldShowSeparator)
                {
                    EnsureHistoryTextStyle(separator, HistoryTextColor);
                    separator.rectTransform.anchoredPosition = new Vector2(currentX, 0f);
                    separator.rectTransform.sizeDelta = new Vector2(HistorySeparatorWidth, HistoryPanelHeight);
                    currentX += HistorySeparatorWidth;
                }
            }

            for (var separatorIndex = visibleCount - 1; separatorIndex < _historySeparatorViews.Count; separatorIndex++)
            {
                if (separatorIndex < 0)
                {
                    continue;
                }

                _historySeparatorViews[separatorIndex].gameObject.SetActive(false);
            }
        }

        private void BuildHistoryContents(RectTransform panel)
        {
            for (var itemIndex = 0; itemIndex < KeyboardChipLimit; itemIndex++)
            {
                var itemRoot = CreatePanel($"HistoryItem{itemIndex}", panel, new Vector2(HistoryItemWidth, HistoryItemHeight), HistoryItemBackgroundColor);
                itemRoot.rectTransform.anchorMin = new Vector2(0f, 0f);
                itemRoot.rectTransform.anchorMax = new Vector2(0f, 0f);
                itemRoot.rectTransform.pivot = new Vector2(0f, 0f);

                var label = CreateText($"HistoryLabel{itemIndex}", itemRoot.rectTransform, 18, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                EnsureHistoryTextStyle(label, HistoryTextColor);
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.rectTransform.anchorMin = new Vector2(0f, 1f);
                label.rectTransform.anchorMax = new Vector2(0f, 1f);
                label.rectTransform.pivot = new Vector2(0f, 1f);

                var elapsed = CreateText($"HistoryElapsed{itemIndex}", itemRoot.rectTransform, 12, TextAlignmentOptions.BottomLeft, FontStyles.Normal);
                EnsureHistoryTextStyle(elapsed, HistoryTimeColor);
                elapsed.rectTransform.anchorMin = new Vector2(0f, 0f);
                elapsed.rectTransform.anchorMax = new Vector2(0f, 0f);
                elapsed.rectTransform.pivot = new Vector2(0f, 0f);

                itemRoot.gameObject.SetActive(false);
                _historyItemViews.Add(new HistoryItemView
                {
                    root = itemRoot.rectTransform,
                    label = label,
                    elapsed = elapsed,
                });

                if (itemIndex >= KeyboardChipLimit - 1)
                {
                    continue;
                }

                var separator = CreateText($"HistorySeparator{itemIndex}", panel, 18f, TextAlignmentOptions.Center, FontStyles.Bold);
                EnsureHistoryTextStyle(separator, HistoryTextColor);
                separator.text = "→";
                separator.rectTransform.anchorMin = new Vector2(0f, 0f);
                separator.rectTransform.anchorMax = new Vector2(0f, 0f);
                separator.rectTransform.pivot = new Vector2(0f, 0f);
                separator.gameObject.SetActive(false);
                _historySeparatorViews.Add(separator);
            }
        }

        private static void EnsureHistoryTextStyle(TextMeshProUGUI text, Color color)
        {
            text.font = TMP_Settings.defaultFontAsset;
            text.color = color;
        }

        private void AnchorHistoryPanel(RectTransform rectTransform, OverlayCorner corner)
        {
            var silhouetteHeight = Mathf.Max(GamepadPanelHeight, KeyboardPanelHeight);
            switch (corner)
            {
                case OverlayCorner.TopLeft:
                    AnchorToCorner(rectTransform, corner, DefaultWidgetMargin, DefaultWidgetMargin + HistoryPanelSpacing);
                    break;
                case OverlayCorner.TopRight:
                    AnchorToCorner(rectTransform, corner, DefaultWidgetMargin, DefaultWidgetMargin + HistoryPanelSpacing);
                    break;
                case OverlayCorner.BottomLeft:
                    AnchorToCorner(rectTransform, corner, DefaultWidgetMargin, DefaultWidgetMargin + silhouetteHeight + HistoryPanelSpacing);
                    break;
                default:
                    AnchorToCorner(rectTransform, corner, DefaultWidgetMargin, DefaultWidgetMargin + silhouetteHeight + HistoryPanelSpacing);
                    break;
            }
        }

        private void HideAllHistoryViews()
        {
            for (var viewIndex = 0; viewIndex < _historyItemViews.Count; viewIndex++)
            {
                _historyItemViews[viewIndex].root.gameObject.SetActive(false);
            }

            for (var separatorIndex = 0; separatorIndex < _historySeparatorViews.Count; separatorIndex++)
            {
                _historySeparatorViews[separatorIndex].gameObject.SetActive(false);
            }
        }

        private static void ApplyHistoryItemLayout(HistoryItemView itemView, float width, float anchoredPositionX)
        {
            itemView.root.anchoredPosition = new Vector2(anchoredPositionX, 0f);
            itemView.root.sizeDelta = new Vector2(width, HistoryItemHeight);
            itemView.label.rectTransform.anchoredPosition = new Vector2(HistoryItemHorizontalPadding, -HistoryItemVerticalPadding);
            // 18pt Bold の行高（約 24px）より低い矩形だと Ellipsis が「1 行も入らない」と判定して全文を消す。行高＋余裕で確保する
            itemView.label.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width - (HistoryItemHorizontalPadding * 2f)), 28f);
            itemView.elapsed.rectTransform.anchoredPosition = new Vector2(HistoryItemHorizontalPadding, HistoryItemVerticalPadding);
            itemView.elapsed.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width - (HistoryItemHorizontalPadding * 2f)), 16f);
        }

        /// <summary>実入力と疑似操作を同じ履歴帯へ記録します。</summary>
        internal void AddHistoryEntry(string label, float now)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            _historyEntries.Add(new HistoryEntry
            {
                label = label,
                startedAt = now,
                elapsedTenths = -1,
                cachedElapsedText = "0.0s",
            });

            var historyCount = Mathf.Max(0, _options.historyCount);
            while (_historyEntries.Count > historyCount)
            {
                _historyEntries.RemoveAt(0);
            }
        }

        private struct HistoryEntry
        {
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public string label;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public float startedAt;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public int elapsedTenths;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public string cachedElapsedText;
        }

        private struct HistoryItemView
        {
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public RectTransform root;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public TextMeshProUGUI label;
            /// <summary>入力状態と描画の対応を分割先でも保持します。</summary>
            public TextMeshProUGUI elapsed;
        }
    }
}
#endif
