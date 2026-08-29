#if UNITY_EDITOR
using System.Text;
using UniLab.UI.Focus;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.UI.Focus.Editor
{
    /// <summary>
    /// 再生中のフォーカスグリッドの位相（スタック・有効/無効・現在セル）をテキストで可視化する
    /// エディタウィンドウ。バグ調査時にシーン全体を目視せずに状態を確認できるようにする。
    /// </summary>
    public sealed class FocusNavigationWindow : EditorWindow
    {
        private const double RepaintIntervalSeconds = 0.1d;

        private FocusNavigator _cachedFocusNavigator;
        private double _lastRepaintTime;
        private Vector2 _scrollPosition;

        /// <summary>メニューからウィンドウを開く。</summary>
        [MenuItem("Window/UniLab/Focus Navigation")]
        private static void Open()
        {
            GetWindow<FocusNavigationWindow>("Focus Navigation");
        }

        private void OnEnable()
        {
            EditorApplication.update += HandleEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
        }

        private void HandleEditorUpdate()
        {
            // perf: 毎フレーム Repaint するとエディタが重くなるため、10fps 程度に間引く。
            if (EditorApplication.timeSinceStartup - _lastRepaintTime < RepaintIntervalSeconds)
            {
                return;
            }

            _lastRepaintTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("再生中のみ利用できます。");
                return;
            }

            var focusNavigator = ResolveFocusNavigator();
            if (focusNavigator == null)
            {
                EditorGUILayout.LabelField("FocusNavigator がシーンにありません。");
                return;
            }

            DrawSceneOverlayToggle();
            EditorGUILayout.Space();
            DrawSummary(focusNavigator);
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawGridStack(focusNavigator);
            EditorGUILayout.EndScrollView();
        }

        private FocusNavigator ResolveFocusNavigator()
        {
            // perf: FindAnyObjectByType はコストが高いため、キャッシュが null または破棄済みのときだけ再検索する。
            if (_cachedFocusNavigator == null)
            {
                _cachedFocusNavigator = Object.FindAnyObjectByType<FocusNavigator>();
            }

            return _cachedFocusNavigator;
        }

        private static void DrawSceneOverlayToggle()
        {
            var isEnabled = EditorPrefs.GetBool(FocusNavigationSceneOverlay.EnabledPrefsKey, true);
            var newValue = EditorGUILayout.ToggleLeft("Scene View に矢印を描画する", isEnabled);
            if (newValue != isEnabled)
            {
                EditorPrefs.SetBool(FocusNavigationSceneOverlay.EnabledPrefsKey, newValue);
            }
        }

        private static void DrawSummary(FocusNavigator focusNavigator)
        {
            EditorGUILayout.LabelField("DesiredColumnIndex", focusNavigator.DesiredColumnIndex.ToString());

            var currentSelectedGameObject = EventSystem.current == null
                ? null
                : EventSystem.current.currentSelectedGameObject;
            var currentSelectedName = currentSelectedGameObject == null ? "(none)" : currentSelectedGameObject.name;
            EditorGUILayout.LabelField("Current Selected", currentSelectedName);
        }

        private void DrawGridStack(FocusNavigator focusNavigator)
        {
            var gridStack = focusNavigator.GridStack;
            var activeGrid = focusNavigator.ActiveGrid;
            var currentSelectedGameObject = EventSystem.current == null
                ? null
                : EventSystem.current.currentSelectedGameObject;

            // 短絡評価と out var を併用すると確定代入を満たせないため、先に無効セルで初期化しておく
            var currentCell = FocusCell.Invalid;
            var hasCurrentCell = activeGrid != null
                && currentSelectedGameObject != null
                && activeGrid.TryFindCell(currentSelectedGameObject, out currentCell);

            // グリッドスタックは下から順に積まれているため、上（アクティブ）から表示するために逆順で辿る。
            for (var stackIndex = gridStack.Count - 1; stackIndex >= 0; stackIndex--)
            {
                var grid = gridStack[stackIndex];
                var isActiveGrid = grid == activeGrid;
                DrawGrid(grid, stackIndex, isActiveGrid, isActiveGrid ? currentCell : FocusCell.Invalid);
            }

            if (activeGrid != null && hasCurrentCell)
            {
                EditorGUILayout.Space();
                DrawResolvedDirections(focusNavigator, activeGrid, currentCell);
            }
        }

        private static void DrawGrid(FocusGrid grid, int stackIndex, bool isActiveGrid, FocusCell currentCell)
        {
            var headerLabel = $"#{stackIndex} rows={grid.RowCount} wrap={grid.WrapMode}";
            if (isActiveGrid)
            {
                headerLabel += " [ACTIVE]";
            }

            EditorGUILayout.LabelField(headerLabel, EditorStyles.boldLabel);

            for (var rowIndex = 0; rowIndex < grid.RowCount; rowIndex++)
            {
                DrawRow(grid, rowIndex, currentCell);
            }
        }

        private static void DrawRow(FocusGrid grid, int rowIndex, FocusCell currentCell)
        {
            var columnCount = grid.GetColumnCount(rowIndex);
            var rowLabelBuilder = new StringBuilder();

            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var cell = new FocusCell(rowIndex, columnIndex);
                var selectableName = grid.GetSelectable(cell).gameObject.name;

                if (cell.Equals(currentCell))
                {
                    rowLabelBuilder.Append($">{selectableName}< ");
                }
                else if (!grid.IsEnabled(cell))
                {
                    rowLabelBuilder.Append($"({selectableName}) ");
                }
                else
                {
                    rowLabelBuilder.Append($"{selectableName} ");
                }
            }

            var isCurrentRow = currentCell.IsValid && currentCell.RowIndex == rowIndex;
            var originalColor = GUI.color;
            GUI.color = isCurrentRow ? GetHighlightColor() : originalColor;
            EditorGUILayout.LabelField(rowLabelBuilder.ToString());
            GUI.color = originalColor;
        }

        private static void DrawResolvedDirections(FocusNavigator focusNavigator, FocusGrid activeGrid, FocusCell currentCell)
        {
            EditorGUILayout.LabelField("Resolved Directions", EditorStyles.boldLabel);
            DrawResolvedDirection(activeGrid, focusNavigator.DesiredColumnIndex, currentCell, FocusDirection.Up);
            DrawResolvedDirection(activeGrid, focusNavigator.DesiredColumnIndex, currentCell, FocusDirection.Down);
            DrawResolvedDirection(activeGrid, focusNavigator.DesiredColumnIndex, currentCell, FocusDirection.Left);
            DrawResolvedDirection(activeGrid, focusNavigator.DesiredColumnIndex, currentCell, FocusDirection.Right);
        }

        private static void DrawResolvedDirection(FocusGrid activeGrid, int desiredColumnIndex, FocusCell currentCell, FocusDirection direction)
        {
            var resolvedLabel = activeGrid.TryResolve(currentCell, desiredColumnIndex, direction, out var nextCell)
                ? $"{activeGrid.GetSelectable(nextCell).gameObject.name} ({nextCell.RowIndex},{nextCell.ColumnIndex})"
                : "-";

            EditorGUILayout.LabelField(direction.ToString(), resolvedLabel);
        }

        private static Color GetHighlightColor()
        {
            // プロスキン/ライトスキンのどちらでも視認できる黄色系の色を選ぶ。
            return EditorGUIUtility.isProSkin
                ? new Color(1f, 0.92f, 0.4f)
                : new Color(0.55f, 0.42f, 0f);
        }
    }
}
#endif
