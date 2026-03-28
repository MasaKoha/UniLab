using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI
{
    /// <summary>
    /// 子要素ごとに異なるサイズを許容しつつ、横方向に詰めて折り返す Grid 風レイアウト。
    /// 各子の preferred サイズ（または実サイズ）を使って配置位置を計算する。
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu( "Layout/Variable Grid Layout Group" )]
    public sealed class VariableGridLayoutGroup : LayoutGroup
    {
        [Header( "Layout" )]
        // 要素間の余白（x: 横、y: 縦）
        [SerializeField] private Vector2 _spacing = new(12f, 12f);
        // 子サイズが取得できない場合のフォールバックサイズ
        [SerializeField] private Vector2 _defaultItemSize = new(160f, 120f);
        // true のとき LayoutElement などの preferredWidth を優先
        [SerializeField] private bool _usePreferredWidth = true;
        // true のとき LayoutElement などの preferredHeight を優先
        [SerializeField] private bool _usePreferredHeight = true;
        // true のとき child の localScale をサイズ計算に反映
        [SerializeField] private bool _useChildScale = false;
        // 過度に小さい値を防ぐ最小幅
        [Min( 1f ), SerializeField] private float _minItemWidth = 40f;
        // 過度に小さい値を防ぐ最小高さ
        [Min( 1f ), SerializeField] private float _minItemHeight = 40f;

        [Header( "Sync" )]
        // true のとき、同期先 RectTransform の高さをこのレイアウト高さへ合わせる
        [SerializeField] private bool _syncAncestorHeight = false;
        // true のとき、同期先 RectTransform の anchoredPosition.y を合わせる
        [SerializeField] private bool _syncAncestorY = true;
        // true のとき、同期先 RectTransform の anchoredPosition.x を合わせる
        [SerializeField] private bool _syncAncestorX = false;
        // 同期先 RectTransform（未設定時は同期しない）
        [SerializeField] private RectTransform _syncTargetRect = null;

        // レイアウト計算結果（1子要素ぶん）
        private struct ChildLayout
        {
            public RectTransform Rect;
            public int Row;
            public float X;
            public float Y;
            public float Width;
            public float Height;
        }

        private enum Align
        {
            Start,
            Center,
            End
        }

        private readonly List<ChildLayout> _childLayouts = new();
        private readonly List<float> _rowWidths = new();
        private readonly List<float> _rowHeights = new();
        private readonly List<float> _rowOffsetXs = new();

        // 全行を積み上げた結果のコンテンツサイズ（padding を除く）
        private float _contentWidth;
        private float _contentHeight;
        private float _lastSyncedAncestorHeight = -1f;
        private float _lastSyncedAncestorY = float.NaN;
        private float _lastSyncedAncestorX = float.NaN;
        private bool _isLayoutCacheDirty = true;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            EnsureLayoutCache();
            // ContentSizeFitter 等が参照する preferred サイズを通知
            var preferred = padding.horizontal + _contentWidth;
            SetLayoutInputForAxis( preferred, preferred, -1f, 0 );
        }

        public override void CalculateLayoutInputVertical()
        {
            EnsureLayoutCache();
            // ContentSizeFitter 等が参照する preferred サイズを通知
            var preferred = padding.vertical + _contentHeight;
            SetLayoutInputForAxis( preferred, preferred, -1f, 1 );
        }

        public override void SetLayoutHorizontal()
        {
            ApplyLayout( 0 );
        }

        public override void SetLayoutVertical()
        {
            ApplyLayout( 1 );
            SyncTargetTransformIfNeeded();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            MarkLayoutCacheDirty();
            SetDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkLayoutCacheDirty();
            SetDirty();
        }

        protected override void OnTransformChildrenChanged()
        {
            base.OnTransformChildrenChanged();
            MarkLayoutCacheDirty();
            SetDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            // Inspector 値の安全化
            _spacing.x = Mathf.Max( 0f, _spacing.x );
            _spacing.y = Mathf.Max( 0f, _spacing.y );
            _defaultItemSize.x = Mathf.Max( 1f, _defaultItemSize.x );
            _defaultItemSize.y = Mathf.Max( 1f, _defaultItemSize.y );
            _minItemWidth = Mathf.Max( 1f, _minItemWidth );
            _minItemHeight = Mathf.Max( 1f, _minItemHeight );
            MarkLayoutCacheDirty();
            SetDirty();
        }
#endif

        public void EnableAncestorHeightSync()
        {
            if (_syncAncestorHeight)
            {
                return;
            }

            _syncAncestorHeight = true;
            SetDirty();
        }

        public void EnableAncestorYSync()
        {
            if (_syncAncestorY)
            {
                return;
            }

            _syncAncestorY = true;
            SetDirty();
        }

        public void EnableAncestorXSync()
        {
            if (_syncAncestorX)
            {
                return;
            }

            _syncAncestorX = true;
            SetDirty();
        }

        public void SetSyncTargetRect( RectTransform target )
        {
            if (_syncTargetRect == target)
            {
                return;
            }

            _syncTargetRect = target;
            SetDirty();
        }

        private void SyncTargetTransformIfNeeded()
        {
            if (!_syncAncestorHeight && !_syncAncestorY && !_syncAncestorX)
            {
                return;
            }

            var target = GetSyncTargetRectTransform();
            if (target == null)
            {
                return;
            }

            var updated = false;

            if (_syncAncestorHeight)
            {
                // 計算済みコンテンツ高さ（padding込み）を使って同期する
                var height = Mathf.Max( 1f, padding.vertical + _contentHeight );
                if (Mathf.Abs( _lastSyncedAncestorHeight - height ) >= 0.1f)
                {
                    _lastSyncedAncestorHeight = height;
                    target.SetSizeWithCurrentAnchors( RectTransform.Axis.Vertical, height );
                    updated = true;
                }
            }

            var sourcePos = rectTransform.anchoredPosition;
            updated |= TrySyncPositionY( target, sourcePos.y );
            updated |= TrySyncPositionX( target, sourcePos.x );

            if (updated)
            {
                // 同期先だけ最小限再ビルドする（連鎖再ビルドで戻る現象を防ぐ）
                LayoutRebuilder.MarkLayoutForRebuild( target );
            }
        }

        private RectTransform GetSyncTargetRectTransform()
        {
            return _syncTargetRect;
        }

        private bool TrySyncPositionX( RectTransform target, float x )
        {
            if (!_syncAncestorX)
            {
                return false;
            }

            if (!float.IsNaN( _lastSyncedAncestorX ) && Mathf.Abs( _lastSyncedAncestorX - x ) < 0.1f)
            {
                return false;
            }

            _lastSyncedAncestorX = x;
            var pos = target.anchoredPosition;
            target.anchoredPosition = new Vector2( x, pos.y );
            return true;
        }

        private bool TrySyncPositionY( RectTransform target, float y )
        {
            if (!_syncAncestorY)
            {
                return false;
            }

            if (!float.IsNaN( _lastSyncedAncestorY ) && Mathf.Abs( _lastSyncedAncestorY - y ) < 0.1f)
            {
                return false;
            }

            _lastSyncedAncestorY = y;
            var pos = target.anchoredPosition;
            target.anchoredPosition = new Vector2( pos.x, y );
            return true;
        }

        private void MarkLayoutCacheDirty()
        {
            _isLayoutCacheDirty = true;
        }

        private void EnsureLayoutCache()
        {
            if (!_isLayoutCacheDirty)
            {
                return;
            }

            RebuildLayoutCache();
            _isLayoutCacheDirty = false;
        }

        /// <summary>
        /// 全子要素の「行番号・行内位置・サイズ」を算出してキャッシュする。
        /// </summary>
        private void RebuildLayoutCache()
        {
            _childLayouts.Clear();
            _rowWidths.Clear();
            _rowHeights.Clear();
            _rowOffsetXs.Clear();

            var innerWidth = ResolveInnerWidth();
            var cursor = new RowCursor();

            for (var i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                var width = ResolveChildSize( child, axis: 0 );
                var height = ResolveChildSize( child, axis: 1 );

                // 現在行に収まらないときは次行へ折り返し
                if (cursor.ShouldWrap( width, innerWidth ))
                {
                    CommitCurrentRow( ref cursor );
                }

                _childLayouts.Add( new ChildLayout
                {
                    Rect = child,
                    Row = cursor.Row,
                    X = cursor.X,
                    Y = cursor.Y,
                    Width = width,
                    Height = height
                } );

                cursor.Advance( width, height, _spacing.x );
            }

            if (_childLayouts.Count > 0)
            {
                _rowWidths.Add( cursor.RowWidth );
                _rowHeights.Add( cursor.RowHeight );
            }

            ComputeContentSize();
        }

        private float ResolveInnerWidth()
        {
            var inner = rectTransform.rect.width - padding.horizontal;
            // Fallback for the transient zero-width state during initial layout pass.
            return inner > 0f ? inner : _defaultItemSize.x;
        }

        private void CommitCurrentRow( ref RowCursor cursor )
        {
            _rowWidths.Add( cursor.RowWidth );
            _rowHeights.Add( cursor.RowHeight );
            cursor.StartNextRow( _spacing.y );
        }

        private void ComputeContentSize()
        {
            _contentWidth = 0f;
            _contentHeight = 0f;

            for (var row = 0; row < _rowWidths.Count; row++)
            {
                _contentWidth = Mathf.Max( _contentWidth, _rowWidths[row] );
                _contentHeight += _rowHeights[row];
            }

            if (_rowHeights.Count > 1)
            {
                _contentHeight += _spacing.y * (_rowHeights.Count - 1);
            }
        }

        // Tracks mutable row-cursor state across the layout loop.
        private struct RowCursor
        {
            public float X;
            public float Y;
            public float RowWidth;
            public float RowHeight;
            public int Row;

            public bool ShouldWrap( float childWidth, float innerWidth )
            {
                return RowWidth > 0f && X + childWidth > innerWidth;
            }

            public void Advance( float childWidth, float childHeight, float spacingX )
            {
                X += childWidth + spacingX;
                RowWidth = Mathf.Max( RowWidth, X - spacingX );
                RowHeight = Mathf.Max( RowHeight, childHeight );
            }

            public void StartNextRow( float spacingY )
            {
                Y += RowHeight + spacingY;
                X = 0f;
                RowWidth = 0f;
                RowHeight = 0f;
                Row++;
            }
        }

        /// <summary>
        /// キャッシュ済みレイアウト情報を使って実際の子座標・サイズを反映する。
        /// axis=0 で X/幅、axis=1 で Y/高さを設定。
        /// </summary>
        private void ApplyLayout( int axis )
        {
            EnsureLayoutCache();

            // 配置対象領域（padding を除く）
            var innerWidth = rectTransform.rect.width - padding.horizontal;
            var innerHeight = rectTransform.rect.height - padding.vertical;

            // 全体の縦方向アライメント補正
            var contentOffsetY = GetAlignmentOffset( innerHeight, _contentHeight, GetVerticalAlign() );
            PrepareRowOffsets( innerWidth );

            for (var i = 0; i < _childLayouts.Count; i++)
            {
                var layout = _childLayouts[i];
                var rowOffsetX = _rowOffsetXs[layout.Row];

                var posX = padding.left + rowOffsetX + layout.X;
                var posY = padding.top + contentOffsetY + layout.Y;

                if (axis == 0)
                {
                    SetChildAlongAxis( layout.Rect, 0, posX, layout.Width );
                }
                else
                {
                    SetChildAlongAxis( layout.Rect, 1, posY, layout.Height );
                }
            }
        }

        private void PrepareRowOffsets( float innerWidth )
        {
            _rowOffsetXs.Clear();
            var horizontalAlign = GetHorizontalAlign();
            for (var row = 0; row < _rowWidths.Count; row++)
            {
                _rowOffsetXs.Add( GetAlignmentOffset( innerWidth, _rowWidths[row], horizontalAlign ) );
            }
        }

        /// <summary>
        /// 子要素の実レイアウトサイズを取得する。
        /// preferred 優先/実サイズ優先の切り替え、scale 反映、最低サイズ保証を行う。
        /// </summary>
        private float ResolveChildSize( RectTransform child, int axis )
        {
            float size;
            var usePreferred = axis == 0 ? _usePreferredWidth : _usePreferredHeight;

            if (usePreferred)
            {
                size = LayoutUtility.GetPreferredSize( child, axis );
            }
            else
            {
                size = axis == 0 ? child.rect.width : child.rect.height;
            }

            if (_useChildScale)
            {
                // スケールをレイアウト計算に反映したいケース向け
                var scale = child.localScale;
                size *= axis == 0 ? scale.x : scale.y;
            }

            // 異常値や 0 以下はフォールバック
            if (float.IsNaN( size ) || float.IsInfinity( size ) || size <= 0f)
            {
                size = axis == 0 ? _defaultItemSize.x : _defaultItemSize.y;
            }

            // 最低サイズを保証
            return axis == 0 ? Mathf.Max( _minItemWidth, size ) : Mathf.Max( _minItemHeight, size );
        }

        // TextAnchor を横位置 enum に変換（Left/Center/Right）
        private Align GetHorizontalAlign()
        {
            return ((int)childAlignment % 3) switch
            {
                1 => Align.Center,
                2 => Align.End,
                _ => Align.Start
            };
        }

        // TextAnchor を縦位置 enum に変換（Upper/Middle/Lower）
        private Align GetVerticalAlign()
        {
            return ((int)childAlignment / 3) switch
            {
                1 => Align.Center,
                2 => Align.End,
                _ => Align.Start
            };
        }

        /// <summary>
        /// 余剰領域に対する開始オフセットを返す。
        /// align: Start=前寄せ, Center=中央, End=後ろ寄せ
        /// </summary>
        private static float GetAlignmentOffset( float available, float required, Align align )
        {
            var surplus = available - required;
            if (surplus <= 0f)
            {
                return 0f;
            }

            return align switch
            {
                Align.Center => surplus * 0.5f,
                Align.End => surplus,
                _ => 0f
            };
        }
    }
}
