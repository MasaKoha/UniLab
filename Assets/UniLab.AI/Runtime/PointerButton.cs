#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// ポインタ入力の語彙をゲームパッド語彙と分け、クリック系 JSON を明示的に保つための列挙です。
    /// </summary>
    public enum PointerButton
    {
        None = 0,
        Left = 1,
        Right = 2,
        Middle = 3,
    }
}
#endif
