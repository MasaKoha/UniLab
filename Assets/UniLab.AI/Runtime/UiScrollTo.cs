#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.AI
{
    /// <summary>対象矩形を祖先のスクロール表示範囲へ最小移動で収めます。</summary>
    internal static class UiScrollTo
    {
        private const int RectangleCornerCount = 4;

        /// <summary>対象を観測してスクロールし、フォーカスを保持したまま結果を返します。</summary>
        internal static bool Execute(string targetSpecification, out string message)
        {
            var target = UiInputLocator.FindTarget(targetSpecification);
            if (target == null || !(target.transform is RectTransform targetRectangle))
            {
                message = $"scrollTo 対象が見つかりません。 target={targetSpecification}";
                return false;
            }

            var ancestors = ObserveAncestors(targetRectangle);
            if (ancestors.Count == 0)
            {
                message = "ScrollRect がありません";
                return false;
            }

            Canvas.ForceUpdateCanvases();
            foreach (var scrollRectangle in ancestors)
            {
                MoveIntoViewport(targetRectangle, scrollRectangle);
            }

            message = "scrollTo を実行しました。";
            return true;
        }

        private static List<ScrollRect> ObserveAncestors(RectTransform target)
        {
            var scrollRectangles = Object.FindObjectsByType<ScrollRect>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var byTransform = new Dictionary<Transform, ScrollRect>();
            foreach (var scrollRectangle in scrollRectangles)
            {
                byTransform[scrollRectangle.transform] = scrollRectangle;
            }

            var ancestors = new List<ScrollRect>();
            for (var ancestor = target.parent; ancestor != null; ancestor = ancestor.parent)
            {
                if (byTransform.TryGetValue(ancestor, out var scrollRectangle)
                    && scrollRectangle.content != null && target.IsChildOf(scrollRectangle.content))
                {
                    ancestors.Add(scrollRectangle);
                }
            }

            return ancestors;
        }

        private static void MoveIntoViewport(RectTransform target, ScrollRect scrollRectangle)
        {
            var viewport = scrollRectangle.viewport != null ? scrollRectangle.viewport : (RectTransform)scrollRectangle.transform;
            var corners = new Vector3[RectangleCornerCount];
            target.GetWorldCorners(corners);
            var minimum = viewport.InverseTransformPoint(corners[0]);
            var maximum = minimum;
            foreach (var corner in corners)
            {
                var localCorner = viewport.InverseTransformPoint(corner);
                minimum = Vector3.Min(minimum, localCorner);
                maximum = Vector3.Max(maximum, localCorner);
            }

            var viewportRectangle = viewport.rect;
            var movement = new Vector3(
                scrollRectangle.horizontal ? ResolveMovement(minimum.x, maximum.x, viewportRectangle.xMin, viewportRectangle.xMax) : 0f,
                scrollRectangle.vertical ? ResolveMovement(minimum.y, maximum.y, viewportRectangle.yMin, viewportRectangle.yMax) : 0f,
                0f);
            var content = scrollRectangle.content;
            var parentMovement = content.parent.InverseTransformVector(viewport.TransformVector(movement));
            scrollRectangle.StopMovement();
            content.anchoredPosition += new Vector2(parentMovement.x, parentMovement.y);
        }

        /// <summary>全体が収まらない大きな対象は、表示範囲を覆う位置まで最小移動します。</summary>
        internal static float ResolveMovement(float targetMinimum, float targetMaximum, float viewportMinimum, float viewportMaximum)
        {
            var towardMinimum = viewportMinimum - targetMinimum;
            var towardMaximum = viewportMaximum - targetMaximum;
            if (towardMinimum > 0f && towardMaximum > 0f)
            {
                return Mathf.Min(towardMinimum, towardMaximum);
            }

            if (towardMinimum < 0f && towardMaximum < 0f)
            {
                return Mathf.Max(towardMinimum, towardMaximum);
            }

            return 0f;
        }
    }
}
#endif
