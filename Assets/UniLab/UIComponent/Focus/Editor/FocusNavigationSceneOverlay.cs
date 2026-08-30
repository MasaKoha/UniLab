#if UNITY_EDITOR
using UniLab.UI.Focus;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.UI.Focus.Editor
{
    /// <summary>
    /// 再生中のフォーカスグリッドの位相を Scene View に矩形とアクティブセルの解決先の線で
    /// 描画するオーバーレイ。<see cref="FocusNavigationWindow"/> のテキスト表示と併用し、
    /// 空間的な位置関係を目視で確認できるようにする。
    /// </summary>
    [InitializeOnLoad]
    public static class FocusNavigationSceneOverlay
    {
        /// <summary>Scene View への描画 ON/OFF を保存する EditorPrefs キー。FocusNavigationWindow と共有する。</summary>
        public const string EnabledPrefsKey = "UniLab.Focus.SceneOverlay";

        private const double RepaintIntervalSeconds = 0.1d;
        private const float ArrowHeadLength = 8f;

        private static FocusNavigator _cachedFocusNavigator;
        private static double _lastRepaintTime;

        static FocusNavigationSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.update += HandleEditorUpdate;
        }

        private static void HandleEditorUpdate()
        {
            // 描画しない状況で Scene View を再描画し続けると、この拡張を入れただけで
            // エディタが常時重くなってしまう。OnSceneGui と同じ条件で先に打ち切る。
            if (!EditorApplication.isPlaying || !EditorPrefs.GetBool(EnabledPrefsKey, true))
            {
                return;
            }

            // perf: SceneView.RepaintAll を毎フレーム呼ぶとエディタが重くなるため、10fps 程度に間引く。
            if (EditorApplication.timeSinceStartup - _lastRepaintTime < RepaintIntervalSeconds)
            {
                return;
            }

            _lastRepaintTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (!EditorPrefs.GetBool(EnabledPrefsKey, true))
            {
                return;
            }

            var focusNavigator = ResolveFocusNavigator();
            if (focusNavigator == null)
            {
                return;
            }

            var activeGrid = focusNavigator.ActiveGrid;
            if (activeGrid == null)
            {
                return;
            }

            var currentSelectedGameObject = EventSystem.current == null
                ? null
                : EventSystem.current.currentSelectedGameObject;
            // 短絡評価と out var を併用すると確定代入を満たせないため、先に無効セルで初期化しておく
            var currentCell = FocusCell.Invalid;
            var hasCurrentCell = currentSelectedGameObject != null
                && activeGrid.TryFindCell(currentSelectedGameObject, out currentCell);

            DrawGridCells(activeGrid, currentCell);

            if (hasCurrentCell)
            {
                DrawResolvedDirectionLines(focusNavigator, activeGrid, currentCell);
            }
        }

        private static FocusNavigator ResolveFocusNavigator()
        {
            // perf: FindAnyObjectByType はコストが高いため、キャッシュが null または破棄済みのときだけ再検索する。
            if (_cachedFocusNavigator == null)
            {
                _cachedFocusNavigator = Object.FindAnyObjectByType<FocusNavigator>();
            }

            return _cachedFocusNavigator;
        }

        private static void DrawGridCells(FocusGrid activeGrid, FocusCell currentCell)
        {
            for (var rowIndex = 0; rowIndex < activeGrid.RowCount; rowIndex++)
            {
                var columnCount = activeGrid.GetColumnCount(rowIndex);
                for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    var cell = new FocusCell(rowIndex, columnIndex);
                    DrawCellRectangle(activeGrid, cell, cell.Equals(currentCell));
                }
            }
        }

        private static void DrawCellRectangle(FocusGrid activeGrid, FocusCell cell, bool isCurrentCell)
        {
            var selectable = activeGrid.GetSelectable(cell);
            var rectTransform = selectable.transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            var worldCorners = new Vector3[4];
            rectTransform.GetWorldCorners(worldCorners);

            // 「非アクティブ」「押せない（枠は乗る）」「通常」の3状態を区別するため、FocusGrid の有効判定ではなく
            // Selectable の実状態を直接見る。現在セルはこれらの状態に関わらず黄色で強調する。
            var isInactive = !selectable.gameObject.activeInHierarchy;
            var isNonInteractable = !isInactive && !selectable.IsInteractable();

            var faceColor = ResolveFaceColor(isCurrentCell, isInactive, isNonInteractable);
            var outlineColor = ResolveOutlineColor(isCurrentCell, isInactive, isNonInteractable);
            Handles.DrawSolidRectangleWithOutline(worldCorners, faceColor, outlineColor);

            var labelPosition = (worldCorners[0] + worldCorners[2]) * 0.5f;
            Handles.Label(labelPosition, $"({cell.RowIndex},{cell.ColumnIndex})");
        }

        private static Color ResolveFaceColor(bool isCurrentCell, bool isInactive, bool isNonInteractable)
        {
            if (isCurrentCell)
            {
                return new Color(1f, 0.92f, 0.1f, 0.35f);
            }

            if (isInactive)
            {
                return new Color(0.3f, 0.3f, 0.3f, 0.2f);
            }

            if (isNonInteractable)
            {
                return new Color(0.7f, 0.5f, 0.2f, 0.2f);
            }

            return new Color(0f, 1f, 1f, 0.2f);
        }

        private static Color ResolveOutlineColor(bool isCurrentCell, bool isInactive, bool isNonInteractable)
        {
            if (isCurrentCell)
            {
                return new Color(1f, 0.92f, 0.1f, 1f);
            }

            if (isInactive)
            {
                return new Color(0.3f, 0.3f, 0.3f, 0.6f);
            }

            if (isNonInteractable)
            {
                return new Color(0.7f, 0.5f, 0.2f, 0.8f);
            }

            return new Color(0f, 1f, 1f, 0.8f);
        }

        private static void DrawResolvedDirectionLines(FocusNavigator focusNavigator, FocusGrid activeGrid, FocusCell currentCell)
        {
            var currentCenter = GetCellCenter(activeGrid, currentCell);
            DrawResolvedDirectionLine(focusNavigator, activeGrid, currentCell, currentCenter, FocusDirection.Up);
            DrawResolvedDirectionLine(focusNavigator, activeGrid, currentCell, currentCenter, FocusDirection.Down);
            DrawResolvedDirectionLine(focusNavigator, activeGrid, currentCell, currentCenter, FocusDirection.Left);
            DrawResolvedDirectionLine(focusNavigator, activeGrid, currentCell, currentCenter, FocusDirection.Right);
        }

        private static void DrawResolvedDirectionLine(FocusNavigator focusNavigator, FocusGrid activeGrid, FocusCell currentCell, Vector3 currentCenter, FocusDirection direction)
        {
            if (!activeGrid.TryResolve(currentCell, focusNavigator.DesiredColumnIndex, direction, focusNavigator.FocusNonInteractable, out var nextCell))
            {
                return;
            }

            var nextCenter = GetCellCenter(activeGrid, nextCell);
            var lineColor = Color.magenta;
            Handles.color = lineColor;
            Handles.DrawAAPolyLine(3f, currentCenter, nextCenter);
            DrawArrowHead(currentCenter, nextCenter, lineColor);
        }

        private static Vector3 GetCellCenter(FocusGrid activeGrid, FocusCell cell)
        {
            var selectable = activeGrid.GetSelectable(cell);
            var rectTransform = selectable.transform as RectTransform;
            var worldCorners = new Vector3[4];
            rectTransform.GetWorldCorners(worldCorners);
            return (worldCorners[0] + worldCorners[2]) * 0.5f;
        }

        private static void DrawArrowHead(Vector3 fromPosition, Vector3 toPosition, Color lineColor)
        {
            // 矢じりの簡易実装: 線分の方向を基準に左右へ振った2本の短い線で表現する。
            var direction = (toPosition - fromPosition).normalized;
            var rightWing = Quaternion.Euler(0f, 0f, 150f) * direction * ArrowHeadLength;
            var leftWing = Quaternion.Euler(0f, 0f, -150f) * direction * ArrowHeadLength;

            Handles.color = lineColor;
            Handles.DrawAAPolyLine(3f, toPosition, toPosition + rightWing);
            Handles.DrawAAPolyLine(3f, toPosition, toPosition + leftWing);
        }
    }
}
#endif
