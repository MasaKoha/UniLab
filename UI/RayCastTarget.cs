#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class RayCastTarget : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(RayCastTarget))]
        private sealed class RayCastTargetEditor : Editor
        {
            public override void OnInspectorGUI()
            {
            }
        }
#endif
    }
}