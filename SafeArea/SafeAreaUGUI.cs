using UnityEngine;

namespace UniLab.SafeArea
{
    // https://trs-game-techblog.info/entry/ugui-safearea/
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaUGUI : MonoBehaviour
    {
        [System.Flags]
        public enum Edge
        {
            Left = 1,
            Right = 2,
            Top = 4,
            Bottom = 8,
        }

        [SerializeField] private Edge _controlEdges = (Edge)~0;

        public Edge ControlEdges => _controlEdges;

        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;
        private Edge _lastControlEdge;
#if UNITY_EDITOR
        private DrivenRectTransformTracker _drivenRectTransformTracker;
#endif

        private void Update()
        {
            Apply();
        }

        private void OnEnable()
        {
            Apply(force: true);
        }

#if UNITY_EDITOR
        private void OnDisable()
        {
            _drivenRectTransformTracker.Clear();
        }
#endif

        private void Apply(bool force = false)
        {
            var rectTransform = (RectTransform)transform;
            var safeArea = Screen.safeArea;
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (resolution.x == 0 || resolution.y == 0)
            {
                return;
            }

            if (!force)
            {
                if (rectTransform.anchorMax == Vector2.zero)
                {
                    // Do apply.
                    // ※Undoすると0になるので再適用させる
                }
                else if (_lastSafeArea == safeArea && _lastResolution == resolution &&
                         _lastControlEdge == _controlEdges)
                {
                    return;
                }
            }

            _lastSafeArea = safeArea;
            _lastResolution = resolution;
            _lastControlEdge = _controlEdges;

#if UNITY_EDITOR
            _drivenRectTransformTracker.Clear();
            _drivenRectTransformTracker.Add(
                this,
                rectTransform,
                DrivenTransformProperties.AnchoredPosition
                | DrivenTransformProperties.SizeDelta
                | DrivenTransformProperties.AnchorMin
                | DrivenTransformProperties.AnchorMax
            );
#endif

            var normalizedMin = new Vector2(safeArea.xMin / resolution.x, safeArea.yMin / resolution.y);
            var normalizedMax = new Vector2(safeArea.xMax / resolution.x, safeArea.yMax / resolution.y);
            if ((_controlEdges & Edge.Left) == 0)
            {
                normalizedMin.x = 0;
            }

            if ((_controlEdges & Edge.Right) == 0)
            {
                normalizedMax.x = 1;
            }

            if ((_controlEdges & Edge.Top) == 0)
            {
                normalizedMax.y = 1;
            }

            if ((_controlEdges & Edge.Bottom) == 0)
            {
                normalizedMin.y = 0;
            }

            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchorMin = normalizedMin;
            rectTransform.anchorMax = normalizedMax;
        }
    }
}