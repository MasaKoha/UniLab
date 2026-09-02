using System;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// ロード遅延を吸収しつつ同じ意味の瞬間に入力を再生するための待機条件です。
    /// </summary>
    [Serializable]
    public sealed class InputReplayAnchor
    {
        /// <summary>
        /// 画面に特定の文字が出るまで待つことで、ラベル表示完了前の入力を避けるための条件です。
        /// </summary>
        public string waitForText;

        /// <summary>
        /// 押したい要素が前面に出るまで待つことで、ロードやフェード中の空打ちを防ぐための条件です。
        /// </summary>
        public string waitForObject;

        /// <summary>
        /// フォーカス前提の UI で移動完了を同期するための条件です。
        /// </summary>
        public string waitForFocus;

        /// <summary>
        /// シーン到着でロード完了を判定し、名前付きシーン遷移に同期するための条件です。
        /// </summary>
        public string waitForScene;
    }
}
#endif
