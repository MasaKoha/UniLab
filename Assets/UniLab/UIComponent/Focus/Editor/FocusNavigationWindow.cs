#if UNITY_EDITOR
using UniLab.UI.Focus;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.UI.Focus.Editor
{
    /// <summary>
    /// 再生中のフォーカスグリッドの位相（スタック・有効/無効・現在セル）を行×列の見た目で可視化する
    /// エディタウィンドウ。バグ調査時にシーン全体を目視せずに状態を確認できるようにする。
    /// </summary>
    public sealed class FocusNavigationWindow : EditorWindow
    {
        private const double RepaintIntervalSeconds = 0.1d;

        private const float CellWidth = 140f;

        /// <summary>セル・バッジ・十字ボックスの枠線の太さ（px）。</summary>
        private const float BorderThickness = 1f;

        /// <summary>ACTIVE バッジの寸法（px）。</summary>
        private const float ActiveBadgeWidth = 62f;
        private const float ActiveBadgeHeight = 16f;
        private const float CellHeight = 22f;
        private const float RowLabelWidth = 30f;
        private const float DirectionPadBoxWidth = 110f;
        private const float DirectionPadBoxHeight = 34f;
        private const float DirectionPadSpacing = 5f;
        private const float ActiveBadgeSpacing = 8f;

        /// <summary>セルの見た目状態。有効/無効の2値では表現しきれない「押せないが枠は乗る」を区別するために持つ。</summary>
        private enum CellState
        {
            /// <summary>通常の押せる状態。</summary>
            Normal,

            /// <summary>現在フォーカス中のセル。</summary>
            Current,

            /// <summary>アクティブだが interactable=false で押せないセル。</summary>
            NonInteractable,

            /// <summary>activeInHierarchy=false でそもそも存在しないセル。</summary>
            Inactive,
        }

        private FocusNavigator _cachedFocusNavigator;
        private double _lastRepaintTime;
        private Vector2 _scrollPosition;

        // perf: GUIStyle は毎フレーム new すると GC 負荷になるため、フィールドに保持して使い回す。
        private GUIStyle _cellBoxStyle;
        private GUIStyle _cellBoxBoldStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _directionPadBoxStyle;

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

            InitializeStylesIfNeeded();

            DrawSceneOverlayToggle();
            EditorGUILayout.Space();
            DrawSummary(focusNavigator);
            EditorGUILayout.Space();

            var activeGrid = focusNavigator.ActiveGrid;
            var currentSelectedGameObject = EventSystem.current == null
                ? null
                : EventSystem.current.currentSelectedGameObject;

            // 短絡評価と out var を併用すると確定代入を満たせないため、先に無効セルで初期化しておく
            var currentCell = FocusCell.Invalid;
            var hasCurrentCell = activeGrid != null
                && currentSelectedGameObject != null
                && activeGrid.TryFindCell(currentSelectedGameObject, out currentCell);

            // 選択自体は存在するのにアクティブグリッドに見つからない状態は、方向キーが効かなくなる実バグの症状なので
            // 一目で分かるように警告を出す。
            if (activeGrid != null && currentSelectedGameObject != null && !hasCurrentCell)
            {
                EditorGUILayout.HelpBox("現在の選択がアクティブグリッドに含まれていません。方向キーは効きません。", MessageType.Warning);
                EditorGUILayout.Space();
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawGridStack(focusNavigator, activeGrid, currentCell, hasCurrentCell);
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

        /// <summary>
        /// GUIStyle は EditorStyles 由来の共有インスタンスを直接書き換えると他の Editor UI に影響するため、
        /// 自分専用のフィールドとして一度だけ複製し、以降のフレームでは使い回す。
        /// </summary>
        private void InitializeStylesIfNeeded()
        {
            if (_cellBoxStyle != null)
            {
                return;
            }

            // 塗りと枠は EditorGUI.DrawRect で自前に描く。GUI.skin.box はエディタスキンだと
            // 背景がほぼ透明で GUI.backgroundColor による色付けが視認できないため、スタイルは
            // 文字の配置だけを担当させる。
            _cellBoxStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                // セル名が長いとボックス幅を押し広げて列が揃わなくなるため、はみ出しは切り詰める。
                clipping = TextClipping.Clip,
            };

            _cellBoxBoldStyle = new GUIStyle(_cellBoxStyle)
            {
                fontStyle = FontStyle.Bold,
            };

            _badgeStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };

            _directionPadBoxStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
        }

        /// <summary>
        /// 塗りつぶし＋1px枠＋中央寄せラベルの矩形を描く。IMGUI の Box スタイルは
        /// エディタスキンでは背景がほとんど出ないため、明示的に矩形を塗って構造を見えるようにする。
        /// </summary>
        private static void DrawFilledBox(Rect area, Color fillColor, Color borderColor, Color textColor, string label, GUIStyle labelStyle)
        {
            EditorGUI.DrawRect(area, fillColor);
            EditorGUI.DrawRect(new Rect(area.x, area.y, area.width, BorderThickness), borderColor);
            EditorGUI.DrawRect(new Rect(area.x, area.yMax - BorderThickness, area.width, BorderThickness), borderColor);
            EditorGUI.DrawRect(new Rect(area.x, area.y, BorderThickness, area.height), borderColor);
            EditorGUI.DrawRect(new Rect(area.xMax - BorderThickness, area.y, BorderThickness, area.height), borderColor);

            var originalTextColor = labelStyle.normal.textColor;
            labelStyle.normal.textColor = textColor;
            GUI.Label(area, label, labelStyle);
            labelStyle.normal.textColor = originalTextColor;
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
            var currentSelectedGameObject = EventSystem.current == null
                ? null
                : EventSystem.current.currentSelectedGameObject;
            var currentSelectedName = currentSelectedGameObject == null ? "(none)" : currentSelectedGameObject.name;
            var focusNonInteractableLabel = focusNavigator.FocusNonInteractable ? "ON" : "OFF";

            var summaryText =
                $"選択中: {currentSelectedName}      列記憶: {focusNavigator.DesiredColumnIndex}      押せない項目もフォーカス: {focusNonInteractableLabel}";
            EditorGUILayout.LabelField(summaryText);
        }

        private void DrawGridStack(FocusNavigator focusNavigator, FocusGrid activeGrid, FocusCell currentCell, bool hasCurrentCell)
        {
            var gridStack = focusNavigator.GridStack;

            // グリッドスタックは下から順に積まれているため、上（アクティブ）から表示するために逆順で辿る。
            for (var stackIndex = gridStack.Count - 1; stackIndex >= 0; stackIndex--)
            {
                var grid = gridStack[stackIndex];
                var isActiveGrid = grid == activeGrid;
                DrawGrid(grid, stackIndex, isActiveGrid, isActiveGrid ? currentCell : FocusCell.Invalid);
            }

            if (activeGrid == null)
            {
                return;
            }

            EditorGUILayout.Space();
            DrawDirectionalPad(focusNavigator, activeGrid, hasCurrentCell ? currentCell : FocusCell.Invalid);
        }

        private void DrawGrid(FocusGrid grid, int stackIndex, bool isActiveGrid, FocusCell currentCell)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawGridHeader(grid, stackIndex, isActiveGrid);

            for (var rowIndex = 0; rowIndex < grid.RowCount; rowIndex++)
            {
                DrawRow(grid, rowIndex, currentCell);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawGridHeader(FocusGrid grid, int stackIndex, bool isActiveGrid)
        {
            EditorGUILayout.BeginHorizontal();

            var headerLabel = $"#{stackIndex}  {grid.RowCount}行  wrap={grid.WrapMode}";
            EditorGUILayout.LabelField(headerLabel, EditorStyles.boldLabel, GUILayout.ExpandWidth(false));

            if (isActiveGrid)
            {
                // 見出しから離れた位置に浮くと見づらいため、固定の狭い間隔だけ空けて直後に置く。
                GUILayout.Space(ActiveBadgeSpacing);
                DrawActiveBadge();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActiveBadge()
        {
            var area = GUILayoutUtility.GetRect(ActiveBadgeWidth, ActiveBadgeHeight, GUILayout.Width(ActiveBadgeWidth), GUILayout.Height(ActiveBadgeHeight));
            var fillColor = EditorGUIUtility.isProSkin ? new Color(0.16f, 0.38f, 0.22f) : new Color(0.72f, 0.92f, 0.76f);
            var borderColor = new Color(0.30f, 0.75f, 0.42f);
            var textColor = EditorGUIUtility.isProSkin ? new Color(0.85f, 1f, 0.88f) : new Color(0.05f, 0.25f, 0.10f);
            DrawFilledBox(area, fillColor, borderColor, textColor, "ACTIVE", _badgeStyle);
        }

        private void DrawRow(FocusGrid grid, int rowIndex, FocusCell currentCell)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"r{rowIndex}", GUILayout.Width(RowLabelWidth));

            var columnCount = grid.GetColumnCount(rowIndex);
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var cell = new FocusCell(rowIndex, columnIndex);
                DrawCell(grid, cell, cell.Equals(currentCell));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCell(FocusGrid grid, FocusCell cell, bool isCurrent)
        {
            var selectable = grid.GetSelectable(cell);
            var state = ResolveCellState(selectable, isCurrent);
            var label = BuildCellLabel(selectable.gameObject.name, state);
            var style = state == CellState.Current ? _cellBoxBoldStyle : _cellBoxStyle;

            // 幅を固定値で揃えることで、行をまたいで列が縦に並んで見えるようにする。
            var area = GUILayoutUtility.GetRect(CellWidth, CellHeight, GUILayout.Width(CellWidth), GUILayout.Height(CellHeight));
            DrawFilledBox(area, GetCellFillColor(state), GetCellBorderColor(state), GetCellTextColor(state), label, style);
        }

        private static CellState ResolveCellState(Selectable selectable, bool isCurrent)
        {
            if (isCurrent)
            {
                return CellState.Current;
            }

            if (!selectable.gameObject.activeInHierarchy)
            {
                return CellState.Inactive;
            }

            if (!selectable.IsInteractable())
            {
                return CellState.NonInteractable;
            }

            return CellState.Normal;
        }

        private static string BuildCellLabel(string selectableName, CellState state)
        {
            switch (state)
            {
                case CellState.NonInteractable:
                    return $"{selectableName} (無効)";
                case CellState.Inactive:
                    return $"({selectableName})";
                default:
                    return selectableName;
            }
        }

        private static Color GetCellFillColor(CellState state)
        {
            var isProSkin = EditorGUIUtility.isProSkin;
            switch (state)
            {
                case CellState.Current:
                    return isProSkin ? new Color(0.42f, 0.34f, 0.07f) : new Color(1f, 0.93f, 0.65f);
                case CellState.NonInteractable:
                    return isProSkin ? new Color(0.33f, 0.26f, 0.18f) : new Color(0.96f, 0.88f, 0.76f);
                case CellState.Inactive:
                    return isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.82f, 0.82f, 0.82f);
                default:
                    return isProSkin ? new Color(0.25f, 0.25f, 0.27f) : new Color(0.92f, 0.92f, 0.94f);
            }
        }

        private static Color GetCellBorderColor(CellState state)
        {
            var isProSkin = EditorGUIUtility.isProSkin;
            switch (state)
            {
                case CellState.Current:
                    return new Color(1f, 0.84f, 0.29f);
                case CellState.NonInteractable:
                    return isProSkin ? new Color(0.55f, 0.44f, 0.30f) : new Color(0.72f, 0.60f, 0.42f);
                case CellState.Inactive:
                    return isProSkin ? new Color(0.30f, 0.30f, 0.30f) : new Color(0.65f, 0.65f, 0.65f);
                default:
                    return isProSkin ? new Color(0.42f, 0.42f, 0.45f) : new Color(0.65f, 0.65f, 0.68f);
            }
        }

        private static Color GetCellTextColor(CellState state)
        {
            var isProSkin = EditorGUIUtility.isProSkin;
            if (state == CellState.Inactive)
            {
                return isProSkin ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.45f, 0.45f, 0.45f);
            }

            return isProSkin ? new Color(0.92f, 0.92f, 0.92f) : new Color(0.10f, 0.10f, 0.10f);
        }

        private void DrawDirectionalPad(FocusNavigator focusNavigator, FocusGrid activeGrid, FocusCell currentCell)
        {
            EditorGUILayout.LabelField("移動先", EditorStyles.boldLabel);

            if (!currentCell.IsValid)
            {
                EditorGUILayout.LabelField("アクティブグリッド内に現在の選択がありません");
                return;
            }

            // 十字の間延びを避けるため、全体を固定幅（3列分の幅+間隔）に収めて上下段を
            // 中央列の真上・真下に GUILayout.Space で揃える。FlexibleSpace は使わない。
            var padTotalWidth = (DirectionPadBoxWidth * 3f) + (DirectionPadSpacing * 2f);
            EditorGUILayout.BeginVertical(GUILayout.Width(padTotalWidth));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(DirectionPadBoxWidth + DirectionPadSpacing);
            DrawDirectionBox(focusNavigator, activeGrid, currentCell, FocusDirection.Up, "↑", includeCoordinates: true);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawDirectionBox(focusNavigator, activeGrid, currentCell, FocusDirection.Left, "←", includeCoordinates: false);
            GUILayout.Space(DirectionPadSpacing);
            DrawCenterBox(activeGrid, currentCell);
            GUILayout.Space(DirectionPadSpacing);
            DrawDirectionBox(focusNavigator, activeGrid, currentCell, FocusDirection.Right, "→", includeCoordinates: false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(DirectionPadBoxWidth + DirectionPadSpacing);
            DrawDirectionBox(focusNavigator, activeGrid, currentCell, FocusDirection.Down, "↓", includeCoordinates: true);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDirectionBox(FocusNavigator focusNavigator, FocusGrid activeGrid, FocusCell currentCell, FocusDirection direction, string arrowSymbol, bool includeCoordinates)
        {
            var canResolve = activeGrid.TryResolve(
                currentCell,
                focusNavigator.DesiredColumnIndex,
                direction,
                focusNavigator.FocusNonInteractable,
                out var nextCell);

            var label = BuildDirectionLabel(activeGrid, arrowSymbol, canResolve, nextCell, includeCoordinates);

            var area = GUILayoutUtility.GetRect(DirectionPadBoxWidth, DirectionPadBoxHeight, GUILayout.Width(DirectionPadBoxWidth), GUILayout.Height(DirectionPadBoxHeight));
            var isProSkin = EditorGUIUtility.isProSkin;

            // 行き止まりは沈んだ色にして、どちらへ行けないかが一目で分かるようにする。
            var fillColor = canResolve
                ? (isProSkin ? new Color(0.25f, 0.25f, 0.27f) : new Color(0.92f, 0.92f, 0.94f))
                : (isProSkin ? new Color(0.15f, 0.15f, 0.16f) : new Color(0.84f, 0.84f, 0.85f));
            var borderColor = canResolve
                ? (isProSkin ? new Color(0.42f, 0.42f, 0.45f) : new Color(0.65f, 0.65f, 0.68f))
                : (isProSkin ? new Color(0.26f, 0.26f, 0.28f) : new Color(0.75f, 0.75f, 0.77f));
            var textColor = canResolve
                ? (isProSkin ? new Color(0.92f, 0.92f, 0.92f) : new Color(0.10f, 0.10f, 0.10f))
                : (isProSkin ? new Color(0.45f, 0.45f, 0.45f) : new Color(0.55f, 0.55f, 0.55f));

            DrawFilledBox(area, fillColor, borderColor, textColor, label, _directionPadBoxStyle);
        }

        private static string BuildDirectionLabel(FocusGrid activeGrid, string arrowSymbol, bool canResolve, FocusCell nextCell, bool includeCoordinates)
        {
            if (!canResolve)
            {
                return arrowSymbol;
            }

            var nextName = activeGrid.GetSelectable(nextCell).gameObject.name;
            return includeCoordinates
                ? $"{arrowSymbol} {nextName}\n({nextCell.RowIndex},{nextCell.ColumnIndex})"
                : $"{arrowSymbol} {nextName}";
        }

        private void DrawCenterBox(FocusGrid activeGrid, FocusCell currentCell)
        {
            var currentName = activeGrid.GetSelectable(currentCell).gameObject.name;
            var area = GUILayoutUtility.GetRect(DirectionPadBoxWidth, DirectionPadBoxHeight, GUILayout.Width(DirectionPadBoxWidth), GUILayout.Height(DirectionPadBoxHeight));
            DrawFilledBox(
                area,
                GetCellFillColor(CellState.Current),
                GetCellBorderColor(CellState.Current),
                GetCellTextColor(CellState.Current),
                currentName,
                _directionPadBoxStyle);
        }






    }
}
#endif
