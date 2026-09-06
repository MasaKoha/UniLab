using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI
{
    /// <summary>
    /// Graphic（Image / TMP など）の頂点色にグラデーションを乗算する汎用エフェクト。
    /// 画像は白1枚でよく、色・方向・割合をコード/Inspector から変えるだけで単色〜グラデを表現できる。
    /// 9-slice でも頂点位置ベースで着色するため滑らかに出る。
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("UniLab/UI/UI Gradient")]
    public sealed class UIGradient : BaseMeshEffect
    {
        /// <summary>グラデの方向。None=0 は無効（エフェクトをかけない）。</summary>
        public enum GradientDirection
        {
            None = 0,
            Vertical = 1,
            Horizontal = 2,
            Angle = 3,
        }

        [SerializeField] private GradientDirection _direction = GradientDirection.Vertical;
        [SerializeField] private Color _colorA = Color.white;
        [SerializeField] private Color _colorB = Color.white;
        [SerializeField, Range(0f, 360f)] private float _angle = 90f;
        [SerializeField, Range(0f, 1f)] private float _start = 0f;
        [SerializeField, Range(0f, 1f)] private float _end = 1f;

        // perf: 全インスタンスで使い回して ModifyMesh のアロケーションを避ける（メインスレッドのみ・再入なし）。
        private static readonly List<UIVertex> SharedStream = new List<UIVertex>();

        /// <summary>グラデの方向。</summary>
        public GradientDirection Direction { get => _direction; set { _direction = value; SetDirty(); } }
        /// <summary>開始色（割合 _start 側）。</summary>
        public Color ColorA { get => _colorA; set { _colorA = value; SetDirty(); } }
        /// <summary>終了色（割合 _end 側）。</summary>
        public Color ColorB { get => _colorB; set { _colorB = value; SetDirty(); } }
        /// <summary>Angle 方向のときの角度（度）。0=右(+X)、90=上(+Y)。</summary>
        public float Angle { get => _angle; set { _angle = value; SetDirty(); } }
        /// <summary>色変化の開始位置（0..1 の割合・CSS カラーストップ相当）。</summary>
        public float Start { get => _start; set { _start = value; SetDirty(); } }
        /// <summary>色変化の終了位置（0..1 の割合）。</summary>
        public float End { get => _end; set { _end = value; SetDirty(); } }

        /// <summary>コードから一括設定する。Presenter やビルダーからの利用を想定。</summary>
        public void Apply(Color colorA, Color colorB, GradientDirection direction = GradientDirection.Vertical, float start = 0f, float end = 1f, float angle = 90f)
        {
            _colorA = colorA;
            _colorB = colorB;
            _direction = direction;
            _start = start;
            _end = end;
            _angle = angle;
            SetDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || _direction == GradientDirection.None)
            {
                return;
            }

            vh.GetUIVertexStream(SharedStream);
            if (SharedStream.Count == 0)
            {
                return;
            }

            var axis = AxisDirection();

            // 軸方向の射影値の範囲を求め、頂点位置を 0..1 に正規化する。
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var i = 0; i < SharedStream.Count; i++)
            {
                var projected = Project(SharedStream[i].position, axis);
                if (projected < min)
                {
                    min = projected;
                }

                if (projected > max)
                {
                    max = projected;
                }
            }

            var range = Mathf.Max(max - min, 1e-5f);
            var span = Mathf.Max(_end - _start, 1e-5f);

            for (var i = 0; i < SharedStream.Count; i++)
            {
                var vertex = SharedStream[i];
                var t = (Project(vertex.position, axis) - min) / range;
                var u = Mathf.Clamp01((t - _start) / span);
                var gradient = Color.Lerp(_colorA, _colorB, u);
                // 既存の Graphic.color / Image.color と合成するため乗算する。
                vertex.color = (Color)vertex.color * gradient;
                SharedStream[i] = vertex;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(SharedStream);
        }

        private Vector2 AxisDirection()
        {
            switch (_direction)
            {
                case GradientDirection.Horizontal:
                    return Vector2.right;
                case GradientDirection.Angle:
                    var radian = _angle * Mathf.Deg2Rad;
                    return new Vector2(Mathf.Cos(radian), Mathf.Sin(radian));
                default:
                    return Vector2.up;
            }
        }

        private static float Project(Vector3 position, Vector2 axis)
        {
            return position.x * axis.x + position.y * axis.y;
        }

        private void SetDirty()
        {
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetDirty();
        }
#endif
    }
}
