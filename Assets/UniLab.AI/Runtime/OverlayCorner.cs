#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// オーバーレイ部品の基準位置を外から切り替えるための列挙です。
    /// 録画対象 UI を隠しにくい配置へ寄せ替えられるようにします。
    /// </summary>
    public enum OverlayCorner
    {
        None = 0,
        TopLeft = 1,
        TopRight = 2,
        BottomLeft = 3,
        BottomRight = 4,
    }
}
#endif
