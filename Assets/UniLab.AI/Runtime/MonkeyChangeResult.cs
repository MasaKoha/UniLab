#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// コルーチンから変化待ち結果を返し、out 引数を使えない制約を避けるための内部モデルです。
    /// </summary>
    public sealed class MonkeyChangeResult
    {
        /// <summary>
        /// スナップショット差分があったかどうかです。
        /// </summary>
        public bool changed;

        /// <summary>
        /// 待機した秒数です。
        /// </summary>
        public float waitedSeconds;
    }
}
#endif
