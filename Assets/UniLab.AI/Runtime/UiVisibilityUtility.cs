#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.AI
{
    /// <summary>
    /// UI 観測で共通になる可視判定・遮蔽判定をまとめます。
    /// 同じ概念を各ツールで別実装にすると観測結果が食い違うためです。
    /// </summary>
    public static class UiVisibilityUtility
    {
        private const int RectangleValueCount = 4;
        private const int RectangleWidthIndex = 2;
        private const int RectangleHeightIndex = 3;

        /// <summary>左下座標・幅・高さで表した要素矩形のうち、クリップ矩形に含まれる面積比を返します。</summary>
        public static float ComputeVisibleRatio(float[] elementRect, float[] clipRect)
        {
            if (elementRect == null || clipRect == null
                || elementRect.Length < RectangleValueCount || clipRect.Length < RectangleValueCount)
            {
                return 0f;
            }

            var elementWidth = elementRect[RectangleWidthIndex];
            var elementHeight = elementRect[RectangleHeightIndex];
            if (elementWidth <= 0f || elementHeight <= 0f)
            {
                return 0f;
            }

            var intersectionWidth = Mathf.Max(0f, Mathf.Min(elementRect[0] + elementWidth,
                clipRect[0] + clipRect[RectangleWidthIndex]) - Mathf.Max(elementRect[0], clipRect[0]));
            var intersectionHeight = Mathf.Max(0f, Mathf.Min(elementRect[1] + elementHeight,
                clipRect[1] + clipRect[RectangleHeightIndex]) - Mathf.Max(elementRect[1], clipRect[1]));
            return Mathf.Clamp01((intersectionWidth / elementWidth) * (intersectionHeight / elementHeight));
        }

        private static readonly Vector3[] WorldCorners = new Vector3[4];

        /// <summary>
        /// オーバーレイ配下を観測対象から外すための判定です。
        /// 観測器を観測結果へ混ぜると 01 と 11 の両方が自己汚染するためです。
        /// </summary>
        public static bool HasOverlayMarkerAncestor(Transform transform)
        {
            var currentTransform = transform;
            while (currentTransform != null)
            {
                if (currentTransform.GetComponent<UiOverlayMarker>() != null)
                {
                    return true;
                }

                currentTransform = currentTransform.parent;
            }

            return false;
        }

        /// <summary>
        /// 表示中の Graphic を持つ対象だけを観測対象へ残す判定です。
        /// 見えない要素まで含めると「画面に何があるか」という目的から外れるためです。
        /// </summary>
        public static bool IsVisibleGraphicObject(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            if (HasOverlayMarkerAncestor(target.transform))
            {
                return false;
            }

            var graphic = target.GetComponent<Graphic>();
            if (graphic == null)
            {
                return false;
            }

            return graphic.enabled;
        }

        /// <summary>
        /// 画面座標の矩形へ変換します。
        /// 録画やスクリーンショットと同じ座標系で後段が読めるようにするためです。
        /// </summary>
        public static bool TryGetScreenRect(RectTransform rectTransform, out float[] rectValues)
        {
            rectValues = null;
            if (rectTransform == null)
            {
                return false;
            }

            rectTransform.GetWorldCorners(WorldCorners);
            var canvasCamera = ResolveCanvasCamera(rectTransform.gameObject);

            var minimumX = float.PositiveInfinity;
            var minimumY = float.PositiveInfinity;
            var maximumX = float.NegativeInfinity;
            var maximumY = float.NegativeInfinity;
            for (var cornerIndex = 0; cornerIndex < WorldCorners.Length; cornerIndex++)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, WorldCorners[cornerIndex]);
                minimumX = Mathf.Min(minimumX, screenPoint.x);
                minimumY = Mathf.Min(minimumY, screenPoint.y);
                maximumX = Mathf.Max(maximumX, screenPoint.x);
                maximumY = Mathf.Max(maximumY, screenPoint.y);
            }

            rectValues = new[]
            {
                minimumX,
                minimumY,
                maximumX - minimumX,
                maximumY - minimumY,
            };
            return true;
        }

        /// <summary>
        /// 対象中心へのレイキャストで最前面要素を調べます。
        /// 見えていても押せない理由をスナップショットへ載せるためです。
        /// </summary>
        public static GameObject FindBlockingObject(GameObject target)
        {
            return FindBlockingObject(target, false);
        }

        /// <summary>非レイキャスト対象の文字でも、祖先背景を除いた最前面 Graphic による遮蔽を返します。</summary>
        internal static GameObject FindTextBlockingObject(GameObject target)
        {
            return FindBlockingObject(target, true);
        }

        private static GameObject FindBlockingObject(GameObject target, bool textElement)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || target == null)
            {
                return null;
            }

            var rectTransform = target.transform as RectTransform;
            if (rectTransform == null)
            {
                return null;
            }

            rectTransform.GetWorldCorners(WorldCorners);
            var worldCenter = (WorldCorners[0] + WorldCorners[2]) * 0.5f;
            var screenPoint = RectTransformUtility.WorldToScreenPoint(ResolveCanvasCamera(target), worldCenter);
            var pointerEventData = new PointerEventData(eventSystem)
            {
                position = screenPoint,
            };

            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerEventData, raycastResults);
            if (raycastResults.Count == 0)
            {
                return null;
            }

            if (textElement)
            {
                return FindTextBlocker(raycastResults, target);
            }

            var frontMostObject = raycastResults[0].gameObject;
            if (IsSelfOrDescendant(frontMostObject, target))
            {
                return null;
            }

            return frontMostObject;
        }

        /// <summary>
        /// Selectable のラベル候補を 1 つ選びます。
        /// 子孫テキストをそのまま並べると親要素と二重計上になるためです。
        /// </summary>
        public static string FindSelectableLabel(GameObject target, int maximumLength)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var textComponents = target.GetComponentsInChildren<TextMeshProUGUI>(false);
            for (var textIndex = 0; textIndex < textComponents.Length; textIndex++)
            {
                var textComponent = textComponents[textIndex];
                if (textComponent == null || !textComponent.enabled)
                {
                    continue;
                }

                if (HasOverlayMarkerAncestor(textComponent.transform))
                {
                    continue;
                }

                var closestSelectable = textComponent.GetComponentInParent<Selectable>();
                if (closestSelectable == null || closestSelectable.gameObject != target)
                {
                    continue;
                }

                return Truncate(textComponent.text, maximumLength);
            }

            return string.Empty;
        }

        /// <summary>
        /// ルートからの階層パスを生成します。
        /// JSON と操作指定で同じ識別子を使えるようにするためです。
        /// </summary>
        public static string BuildPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var pathSegments = new Stack<string>();
            var currentTransform = transform;
            while (currentTransform != null)
            {
                pathSegments.Push(currentTransform.name);
                currentTransform = currentTransform.parent;
            }

            return string.Join("/", pathSegments.ToArray());
        }

        /// <summary>
        /// 表示用に文字列を切り詰めます。
        /// 極端に長い文が 1 要素で出力全体を汚染するのを避けるためです。
        /// </summary>
        public static string Truncate(string text, int maximumLength)
        {
            if (string.IsNullOrEmpty(text) || maximumLength <= 0)
            {
                return string.Empty;
            }

            if (text.Length <= maximumLength)
            {
                return text;
            }

            return text.Substring(0, maximumLength);
        }

        /// <summary>
        /// Selectable の操作可否を統一的に読みます。
        /// 親 CanvasGroup を含む実際の押下可否に合わせるためです。
        /// </summary>
        public static bool IsInteractable(Selectable selectable)
        {
            if (selectable == null)
            {
                return false;
            }

            return selectable.IsInteractable();
        }

        private static GameObject FindTextBlocker(List<RaycastResult> results, GameObject target)
        {
            foreach (var result in results)
            {
                if (!(result.module is GraphicRaycaster) || result.gameObject == null
                    || HasOverlayMarkerAncestor(result.gameObject.transform))
                {
                    continue;
                }

                var candidate = result.gameObject;
                return IsSelfOrDescendant(candidate, target) || IsSelfOrDescendant(target, candidate)
                    ? null : candidate;
            }

            return null;
        }

        private static bool IsSelfOrDescendant(GameObject candidate, GameObject target)
        {
            if (candidate == null || target == null)
            {
                return false;
            }

            var currentTransform = candidate.transform;
            while (currentTransform != null)
            {
                if (currentTransform.gameObject == target)
                {
                    return true;
                }

                currentTransform = currentTransform.parent;
            }

            return false;
        }

        private static Camera ResolveCanvasCamera(GameObject target)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera;
        }
    }
}
#endif
