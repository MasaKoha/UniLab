namespace UniLab.UI.Popup.Sample
{
    /// <summary>
    /// 報酬ポップアップの結果。受け取ったか否かと受領数を返す。
    /// </summary>
    public readonly struct RewardPopupResult
    {
        /// <summary>報酬を受け取ったか。背景タップ / バックキー時は false。</summary>
        public bool Claimed { get; }

        /// <summary>受け取った報酬数。</summary>
        public int Amount { get; }

        /// <summary>受領状態と受領数から結果を生成する。</summary>
        public RewardPopupResult(bool claimed, int amount)
        {
            Claimed = claimed;
            Amount = amount;
        }
    }
}
