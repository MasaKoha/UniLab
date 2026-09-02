#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// セッション全体の終了状態を固定 JSON で残し、外部運転手が成功と失敗を同じ形式で扱えるようにします。
    /// </summary>
    [Serializable]
    public sealed class AgentSessionReport
    {
        /// <summary>セッション識別子です。</summary>
        public string session;

        /// <summary>DebugOutput/agent 配下の出力先です。</summary>
        public string outputDirectory;

        /// <summary>running / reached / maxSteps / maxSeconds / stuck / ended の状態です。</summary>
        public string result;

        /// <summary>開始時刻です。</summary>
        public string startedAt;

        /// <summary>終了時刻です。</summary>
        public string finishedAt;

        /// <summary>実時間の経過秒です。</summary>
        public float durationSeconds;

        /// <summary>実行済み手数です。</summary>
        public int stepCount;

        /// <summary>手数上限です。</summary>
        public int maxSteps;

        /// <summary>実時間上限です。</summary>
        public int maxSeconds;

        /// <summary>達成済みかどうかを自己申告ではなく期待値評価で残します。</summary>
        public bool goalReached;

        /// <summary>停止や拒否の理由を外側へ短く返すための説明です。</summary>
        public string message;

        /// <summary>成功時に書き出した 02 シナリオ JSON のパスです。</summary>
        public string scenario;
    }
}
#endif
