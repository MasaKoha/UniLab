namespace UniLab.UI.Focus
{
    /// <summary>
    /// 方向キー押しっぱなし時のフォーカス移動リピートを自前で計算する。
    /// EventSystem の自動ナビゲーションを廃したことで失うリピート挙動を代替する。
    /// UnityEngine 非依存の純粋クラス。EditMode テストの対象本体。
    /// </summary>
    public sealed class FocusMoveRepeater
    {
        /// <summary>
        /// 残り時間の 0 判定に使う許容誤差。float の累積誤差で 0.4 - 0.1×4 が
        /// わずかに正のまま残り、リピートが1フレーム遅れるのを防ぐ。
        /// </summary>
        private const float FireToleranceSeconds = 0.0001f;

        private readonly float _initialDelaySeconds;
        private readonly float _repeatIntervalSeconds;

        private FocusDirection _previousDirection;
        private float _remainingSecondsUntilNextFire;

        /// <summary>初回発火までの遅延秒数とリピート間隔秒数を指定する。</summary>
        public FocusMoveRepeater(float initialDelaySeconds, float repeatIntervalSeconds)
        {
            _initialDelaySeconds = initialDelaySeconds;
            _repeatIntervalSeconds = repeatIntervalSeconds;
        }

        /// <summary>現フレームの入力方向と経過秒を渡し、この呼び出しでフォーカス移動を発火すべきかを返す。</summary>
        public bool ShouldFire(FocusDirection direction, float deltaTimeSeconds)
        {
            if (direction == FocusDirection.None)
            {
                _previousDirection = FocusDirection.None;
                _remainingSecondsUntilNextFire = 0f;
                return false;
            }

            if (direction != _previousDirection)
            {
                // 方向が変わった直後は即座に発火し、次の発火までは initialDelay を待たせる
                _previousDirection = direction;
                _remainingSecondsUntilNextFire = _initialDelaySeconds;
                return true;
            }

            _remainingSecondsUntilNextFire -= deltaTimeSeconds;
            if (_remainingSecondsUntilNextFire > FireToleranceSeconds)
            {
                return false;
            }

            // 減算方式にすることで、フレームレート変動によるリピート取りこぼしを防ぐ
            _remainingSecondsUntilNextFire += _repeatIntervalSeconds;
            return true;
        }
    }
}
