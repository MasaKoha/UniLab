using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 修正後の再生に必要な周辺情報を入力列と分離し、run 単位で再利用できるようにする manifest です。
    /// </summary>
    [Serializable]
    public sealed class ReplayManifest
    {
        /// <summary>
        /// ディレクトリ名と表示名を一致させ、人間と自動処理の参照先をずらさないための名前です。
        /// </summary>
        public string name;

        /// <summary>
        /// 再生で固定ステップへ戻す値です。記録時のフレーム進行を再利用するため保持します。
        /// </summary>
        public int recordingFramesPerSecond;

        /// <summary>
        /// 録画開始から終了までのフレーム数です。再生完了の目安と整合確認に使います。
        /// </summary>
        public int frameCount;

        /// <summary>
        /// 記録した入力件数です。再生後の件数照合で取りこぼしを検知するため保持します。
        /// </summary>
        public int inputCount;

        /// <summary>
        /// 記録作成時刻です。複数 run の比較時に由来を追えるようにします。
        /// </summary>
        public string recordedAt;

        /// <summary>
        /// 保存済みセーブの参照先です。空でも入力再生自体はできるようにしておきます。
        /// </summary>
        public string saveBeforePath;

        /// <summary>
        /// ゲーム側 seed コマンドの値を持ち運ぶための文字列です。未設定でも許容します。
        /// </summary>
        public string seedValue;

        /// <summary>
        /// 記録時の Unity 版差異を把握し、Input System の将来差分を疑えるようにします。
        /// </summary>
        public string unityVersion;
    }
}
#endif
