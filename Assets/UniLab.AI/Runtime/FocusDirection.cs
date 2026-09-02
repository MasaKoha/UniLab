#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 方向入力を文字列ではなく型で扱い、シナリオ解釈の揺れを防ぐための 4 方向です。
    /// </summary>
    public enum FocusDirection
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
    }
}
#endif
