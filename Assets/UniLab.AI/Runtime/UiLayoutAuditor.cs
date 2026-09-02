using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// UI レイアウトの破綻（はみ出し・重なり）を走査して報告する。
    /// </summary>
    public static class UiLayoutAuditor
    {
        private const string TextOverflowKind = "TextOverflow";
        private const string ClipOverflowKind = "ClipOverflow";
        private const string SiblingOverlapKind = "SiblingOverlap";
        private const float PixelTolerance = 1.0f;
        private const int WorldCornerCount = 4;

        /// <summary>
        /// ロード済み全シーンの Canvas 配下を監査する。
        /// </summary>
        public static UiLayoutAuditReport Audit()
        {
            Canvas.ForceUpdateCanvases();

            var entries = new List<UiLayoutAuditEntry>();
            var canvasList = new List<Canvas>();
            var sceneCount = SceneManager.sceneCount;
            for (var sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var rootGameObjects = scene.GetRootGameObjects();
                for (var rootIndex = 0; rootIndex < rootGameObjects.Length; rootIndex++)
                {
                    rootGameObjects[rootIndex].GetComponentsInChildren(true, canvasList);
                }
            }

            for (var canvasIndex = 0; canvasIndex < canvasList.Count; canvasIndex++)
            {
                var canvas = canvasList[canvasIndex];
                if (!canvas.gameObject.activeInHierarchy)
                {
                    continue;
                }

                AuditCanvas(canvas, entries);
            }

            return new UiLayoutAuditReport
            {
                capturedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                entries = entries.ToArray(),
            };
        }

        private static void AuditCanvas(Canvas canvas, List<UiLayoutAuditEntry> entries)
        {
            var rectTransforms = canvas.GetComponentsInChildren<RectTransform>(true);
            for (var index = 0; index < rectTransforms.Length; index++)
            {
                var rectTransform = rectTransforms[index];
                if (!rectTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!BelongsToCanvas(rectTransform, canvas))
                {
                    continue;
                }

                AuditTextOverflow(rectTransform, entries);
                AuditClipOverflow(rectTransform, entries);
            }

            AuditSiblingOverlaps(canvas, entries);
        }

        private static void AuditTextOverflow(RectTransform rectTransform, List<UiLayoutAuditEntry> entries)
        {
            if (!rectTransform.TryGetComponent<TextMeshProUGUI>(out var textMeshPro))
            {
                return;
            }

            var rect = rectTransform.rect;
            var hasHorizontalOverflow = textMeshPro.textWrappingMode == TextWrappingModes.NoWrap
                && textMeshPro.preferredWidth - rect.width >= PixelTolerance;
            var hasVerticalOverflow = textMeshPro.preferredHeight - rect.height >= PixelTolerance;
            if (!hasHorizontalOverflow && !hasVerticalOverflow)
            {
                return;
            }

            var message = $"preferred=({textMeshPro.preferredWidth:F1}, {textMeshPro.preferredHeight:F1}) rect=({rect.width:F1}, {rect.height:F1})";
            entries.Add(new UiLayoutAuditEntry
            {
                kind = TextOverflowKind,
                path = BuildPath(rectTransform),
                message = message,
            });
        }

        private static void AuditClipOverflow(RectTransform rectTransform, List<UiLayoutAuditEntry> entries)
        {
            if (IsUnderScrollRectContent(rectTransform))
            {
                return;
            }

            if (!TryFindNearestClipAncestor(rectTransform, out var clipRectTransform, out var clipComponentName))
            {
                return;
            }

            var elementBounds = GetWorldRect(rectTransform);
            var clipBounds = GetWorldRect(clipRectTransform);
            if (!IsOutsideBounds(elementBounds, clipBounds))
            {
                return;
            }

            var message = $"element={FormatRect(elementBounds)} clip={FormatRect(clipBounds)} ancestor={clipComponentName}";
            entries.Add(new UiLayoutAuditEntry
            {
                kind = ClipOverflowKind,
                path = BuildPath(rectTransform),
                message = message,
            });
        }

        private static void AuditSiblingOverlaps(Canvas canvas, List<UiLayoutAuditEntry> entries)
        {
            var layoutParents = canvas.GetComponentsInChildren<RectTransform>(true);
            for (var index = 0; index < layoutParents.Length; index++)
            {
                var parentRectTransform = layoutParents[index];
                if (!parentRectTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!BelongsToCanvas(parentRectTransform, canvas))
                {
                    continue;
                }

                if (!HasSupportedLayoutGroup(parentRectTransform))
                {
                    continue;
                }

                var graphicChildren = CollectActiveDirectGraphicChildren(parentRectTransform);
                for (var firstIndex = 0; firstIndex < graphicChildren.Count; firstIndex++)
                {
                    var firstRectTransform = graphicChildren[firstIndex];
                    var firstRect = GetWorldRect(firstRectTransform);
                    for (var secondIndex = firstIndex + 1; secondIndex < graphicChildren.Count; secondIndex++)
                    {
                        var secondRectTransform = graphicChildren[secondIndex];
                        var secondRect = GetWorldRect(secondRectTransform);
                        if (!RectsOverlap(firstRect, secondRect))
                        {
                            continue;
                        }

                        entries.Add(new UiLayoutAuditEntry
                        {
                            kind = SiblingOverlapKind,
                            path = BuildPath(parentRectTransform),
                            message = $"{BuildPath(firstRectTransform)} overlaps {BuildPath(secondRectTransform)} first={FormatRect(firstRect)} second={FormatRect(secondRect)}",
                        });
                    }
                }
            }
        }

        private static bool IsUnderScrollRectContent(RectTransform rectTransform)
        {
            var currentTransform = rectTransform;
            while (currentTransform != null)
            {
                var parentTransform = currentTransform.parent as RectTransform;
                if (parentTransform == null)
                {
                    return false;
                }

                if (parentTransform.TryGetComponent<ScrollRect>(out var scrollRect) && scrollRect.content == currentTransform)
                {
                    return true;
                }

                currentTransform = parentTransform;
            }

            return false;
        }

        private static bool TryFindNearestClipAncestor(RectTransform rectTransform, out RectTransform clipRectTransform, out string clipComponentName)
        {
            var currentTransform = rectTransform.parent as RectTransform;
            while (currentTransform != null)
            {
                if (currentTransform.TryGetComponent<RectMask2D>(out _))
                {
                    clipRectTransform = currentTransform;
                    clipComponentName = nameof(RectMask2D);
                    return true;
                }

                if (currentTransform.TryGetComponent<Mask>(out _))
                {
                    clipRectTransform = currentTransform;
                    clipComponentName = nameof(Mask);
                    return true;
                }

                currentTransform = currentTransform.parent as RectTransform;
            }

            clipRectTransform = null;
            clipComponentName = string.Empty;
            return false;
        }

        private static bool HasSupportedLayoutGroup(RectTransform rectTransform)
        {
            return rectTransform.TryGetComponent<HorizontalLayoutGroup>(out _)
                || rectTransform.TryGetComponent<VerticalLayoutGroup>(out _)
                || rectTransform.TryGetComponent<GridLayoutGroup>(out _);
        }

        private static List<RectTransform> CollectActiveDirectGraphicChildren(RectTransform parentRectTransform)
        {
            var graphicChildren = new List<RectTransform>();
            var childCount = parentRectTransform.childCount;
            for (var childIndex = 0; childIndex < childCount; childIndex++)
            {
                var childRectTransform = parentRectTransform.GetChild(childIndex) as RectTransform;
                if (childRectTransform == null)
                {
                    continue;
                }

                if (!childRectTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!childRectTransform.TryGetComponent<Graphic>(out _))
                {
                    continue;
                }

                graphicChildren.Add(childRectTransform);
            }

            return graphicChildren;
        }

        private static bool BelongsToCanvas(RectTransform rectTransform, Canvas canvas)
        {
            var currentTransform = rectTransform;
            while (currentTransform != null)
            {
                if (currentTransform.TryGetComponent<Canvas>(out var currentCanvas))
                {
                    return currentCanvas == canvas;
                }

                currentTransform = currentTransform.parent as RectTransform;
            }

            return false;
        }

        private static string BuildPath(Transform transform)
        {
            var segments = new List<string>();
            var currentTransform = transform;
            while (currentTransform != null)
            {
                segments.Add(currentTransform.name);
                currentTransform = currentTransform.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            var corners = new Vector3[WorldCornerCount];
            rectTransform.GetWorldCorners(corners);
            var minX = corners[0].x;
            var maxX = corners[0].x;
            var minY = corners[0].y;
            var maxY = corners[0].y;
            for (var cornerIndex = 1; cornerIndex < corners.Length; cornerIndex++)
            {
                var corner = corners[cornerIndex];
                if (corner.x < minX)
                {
                    minX = corner.x;
                }

                if (corner.x > maxX)
                {
                    maxX = corner.x;
                }

                if (corner.y < minY)
                {
                    minY = corner.y;
                }

                if (corner.y > maxY)
                {
                    maxY = corner.y;
                }
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static bool IsOutsideBounds(Rect elementBounds, Rect clipBounds)
        {
            return elementBounds.xMin < clipBounds.xMin - PixelTolerance
                || elementBounds.xMax > clipBounds.xMax + PixelTolerance
                || elementBounds.yMin < clipBounds.yMin - PixelTolerance
                || elementBounds.yMax > clipBounds.yMax + PixelTolerance;
        }

        private static bool RectsOverlap(Rect firstRect, Rect secondRect)
        {
            var horizontalOverlap = firstRect.xMin < secondRect.xMax - PixelTolerance
                && firstRect.xMax > secondRect.xMin + PixelTolerance;
            if (!horizontalOverlap)
            {
                return false;
            }

            return firstRect.yMin < secondRect.yMax - PixelTolerance
                && firstRect.yMax > secondRect.yMin + PixelTolerance;
        }

        private static string FormatRect(Rect rect)
        {
            return $"({rect.x:F1}, {rect.y:F1}, {rect.width:F1}, {rect.height:F1})";
        }
    }
}
#endif
