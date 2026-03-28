using System.Collections;
using NUnit.Framework;
using UniLab.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace UniLab.Tests.PlayMode.UI
{
    /// <summary>
    /// Verifies layout produced by VariableGridLayoutGroup.
    ///
    /// Position assertions use RELATIVE differences between children rather than absolute
    /// anchoredPosition values, because SetChildAlongAxis offsets anchoredPosition by
    /// (size * pivot) which depends on the child's pivot — making absolute values
    /// fragile and pivot-dependent.
    /// </summary>
    public class VariableGridLayoutGroupTest
    {
        private GameObject _canvasGo;
        private RectTransform _container;

        private const float ContainerWidth = 400f;
        private const float ChildWidth = 100f;
        private const float ChildHeight = 50f;
        private const float SpacingX = 10f;
        private const float SpacingY = 8f;

        [SetUp]
        public void SetUp()
        {
            _canvasGo = new GameObject("Canvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasGo.AddComponent<CanvasScaler>();
            _canvasGo.AddComponent<GraphicRaycaster>();

            var containerGo = new GameObject("Container");
            containerGo.transform.SetParent(_canvasGo.transform);
            _container = containerGo.AddComponent<RectTransform>();
            _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ContainerWidth);
            _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 600f);
            _container.anchoredPosition = Vector2.zero;

            var layout = containerGo.AddComponent<VariableGridLayoutGroup>();
            SetPrivateField(layout, "_spacing", new Vector2(SpacingX, SpacingY));
            SetPrivateField(layout, "_defaultItemSize", new Vector2(ChildWidth, ChildHeight));
            SetPrivateField(layout, "_usePreferredWidth", true);
            SetPrivateField(layout, "_usePreferredHeight", true);
            SetPrivateField(layout, "_minItemWidth", 1f);
            SetPrivateField(layout, "_minItemHeight", 1f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null)
            {
                UnityEngine.Object.Destroy(_canvasGo);
            }
        }

        // --- Helpers ---

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private GameObject AddChild(float width, float height)
        {
            var go = new GameObject("Child");
            go.transform.SetParent(_container);
            var rect = go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return go;
        }

        private static Vector2 AnchoredPos(GameObject go)
        {
            return go.GetComponent<RectTransform>().anchoredPosition;
        }

        private void Rebuild()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
        }

        // --- Tests ---

        [UnityTest]
        public IEnumerator NoChildren_ContentHeightIsZero()
        {
            yield return null;
            Rebuild();
            yield return null;

            Assert.AreEqual(0f, LayoutUtility.GetPreferredHeight(_container), 1f);
        }

        [UnityTest]
        public IEnumerator SingleChild_ContentSizeMatchesChildSize()
        {
            AddChild(ChildWidth, ChildHeight);
            yield return null;
            Rebuild();
            yield return null;

            Assert.AreEqual(ChildWidth, LayoutUtility.GetPreferredWidth(_container), 1f,
                "Preferred width should equal the single child width.");
            Assert.AreEqual(ChildHeight, LayoutUtility.GetPreferredHeight(_container), 1f,
                "Preferred height should equal the single child height.");
        }

        [UnityTest]
        public IEnumerator TwoChildrenOnSameRow_XOffsetEqualsWidthPlusSpacing()
        {
            // Both 100-wide children fit on the 400-wide container's first row.
            var child1 = AddChild(ChildWidth, ChildHeight);
            var child2 = AddChild(ChildWidth, ChildHeight);
            yield return null;
            Rebuild();
            yield return null;

            var deltaX = AnchoredPos(child2).x - AnchoredPos(child1).x;
            Assert.AreEqual(ChildWidth + SpacingX, deltaX, 1f,
                "X offset between same-row children should be childWidth + spacingX.");
        }

        [UnityTest]
        public IEnumerator TwoChildrenOnSameRow_HaveEqualY()
        {
            var child1 = AddChild(ChildWidth, ChildHeight);
            var child2 = AddChild(ChildWidth, ChildHeight);
            yield return null;
            Rebuild();
            yield return null;

            Assert.AreEqual(AnchoredPos(child1).y, AnchoredPos(child2).y, 1f,
                "Children on the same row should share Y position.");
        }

        [UnityTest]
        public IEnumerator ChildThatDoesNotFit_WrapsToNextRow_XResetsToFirst()
        {
            // Row 0: child1 (x=0), child2 (x=160) — 150+10+150=310 ≤ 400
            // Row 1: child3 wraps — 310+10+150=470 > 400
            const float w = 150f;
            var child1 = AddChild(w, ChildHeight);
            AddChild(w, ChildHeight);
            var child3 = AddChild(w, ChildHeight);
            yield return null;
            Rebuild();
            yield return null;

            // child3 X should match child1 X (both start at the left of their row).
            Assert.AreEqual(AnchoredPos(child1).x, AnchoredPos(child3).x, 1f,
                "Wrapped child X should match the first child's X (row restart).");
        }

        [UnityTest]
        public IEnumerator ChildThatDoesNotFit_WrapsToNextRow_YOffsetEqualsHeightPlusSpacing()
        {
            const float w = 150f;
            var child1 = AddChild(w, ChildHeight);
            AddChild(w, ChildHeight);
            var child3 = AddChild(w, ChildHeight);
            yield return null;
            Rebuild();
            yield return null;

            // Y distance between rows (in absolute terms) should equal rowHeight + spacingY.
            // LayoutGroup uses top-down layout, so row-1 Y is lesser (more negative) than row-0 Y.
            var rowYDelta = Mathf.Abs(AnchoredPos(child3).y - AnchoredPos(child1).y);
            Assert.AreEqual(ChildHeight + SpacingY, rowYDelta, 1f,
                "Y distance between row 0 and row 1 should be childHeight + spacingY.");
        }

        [UnityTest]
        public IEnumerator TwoRows_PreferredHeightEqualsRowHeightsPlusSpacing()
        {
            // Two 250-wide children wrap into two rows.
            AddChild(250f, ChildHeight);
            AddChild(250f, ChildHeight);
            yield return null;
            Rebuild();
            yield return null;

            var expected = 2f * ChildHeight + SpacingY;
            Assert.AreEqual(expected, LayoutUtility.GetPreferredHeight(_container), 1f,
                "Preferred height should be sum of row heights plus inter-row spacing.");
        }

        [UnityTest]
        public IEnumerator FourChildrenOnSingleRow_XOffsetIncreasesConsistently()
        {
            // 4 * 80 + 3 * 10 = 350 ≤ 400 — all fit on one row.
            const float w = 80f;
            var children = new GameObject[4];
            for (var i = 0; i < 4; i++)
            {
                children[i] = AddChild(w, ChildHeight);
            }

            yield return null;
            Rebuild();
            yield return null;

            for (var i = 1; i < 4; i++)
            {
                var delta = AnchoredPos(children[i]).x - AnchoredPos(children[0]).x;
                Assert.AreEqual(i * (w + SpacingX), delta, 1f,
                    $"Child {i} X offset from child 0 should be {i * (w + SpacingX)}.");
            }
        }

        [UnityTest]
        public IEnumerator FourChildrenOnSingleRow_AllHaveSameY()
        {
            const float w = 80f;
            var children = new GameObject[4];
            for (var i = 0; i < 4; i++)
            {
                children[i] = AddChild(w, ChildHeight);
            }

            yield return null;
            Rebuild();
            yield return null;

            var baseY = AnchoredPos(children[0]).y;
            for (var i = 1; i < 4; i++)
            {
                Assert.AreEqual(baseY, AnchoredPos(children[i]).y, 1f,
                    $"Child {i} should have same Y as child 0 (same row).");
            }
        }
    }
}
