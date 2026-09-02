#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// 観測用オーバーレイ配下を他の観測器から除外する目印です。
    /// 観測器が自分自身を観測結果へ混ぜる循環を避けるために置きます。
    /// </summary>
    public sealed class UiOverlayMarker : MonoBehaviour
    {
    }
}
#endif
