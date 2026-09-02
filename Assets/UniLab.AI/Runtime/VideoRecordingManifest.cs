#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// 録画のフレーム列とマーカーを復元するための manifest です。
    /// </summary>
    [Serializable]
    public sealed class VideoRecordingManifest
    {
        /// <summary>
        /// 録画名です。
        /// </summary>
        public string name;

        /// <summary>
        /// 録画 FPS です。
        /// </summary>
        public int framesPerSecond;

        /// <summary>
        /// 書き出した総フレーム数です。
        /// </summary>
        public int frameCount;

        /// <summary>バッファ不足で撮影を見送ったフレーム数。0 でないなら録画負荷が高い。</summary>
        public int droppedFrameCount;

        /// <summary>録画した実時間の長さ（秒）。動画の再生時間はこれに一致する。</summary>
        public float durationSeconds;

        /// <summary>
        /// 録画幅です。
        /// </summary>
        public int width;

        /// <summary>
        /// 録画高さです。
        /// </summary>
        public int height;

        /// <summary>
        /// スクリーンショット取得時の元フレーム幅です。
        /// crop 前の生画像サイズを後段で復元するため保持します。
        /// </summary>
        public int capturedWidth;

        /// <summary>
        /// スクリーンショット取得時の元フレーム高さです。
        /// crop 前の生画像サイズを後段で復元するため保持します。
        /// </summary>
        public int capturedHeight;

        /// <summary>
        /// 元フレームから切り出した矩形です。
        /// 順序は x, y, width, height です。
        /// </summary>
        public int[] cropRect;

        /// <summary>音声を録音した場合は true。</summary>
        public bool hasAudio;

        /// <summary>音声のサンプリングレート。録音していない場合は 0。</summary>
        public int audioSampleRate;

        /// <summary>音声のチャンネル数。録音していない場合は 0。</summary>
        public int audioChannelCount;

        /// <summary>
        /// 録画へ入力オーバーレイが写っているかを後段が判別できるようにします。
        /// </summary>
        public bool inputOverlay;

        /// <summary>
        /// 録画開始時刻です。
        /// </summary>
        public string startedAtRealtime;

        /// <summary>
        /// mp4 変換用の ffmpeg コマンドです。
        /// </summary>
        public string ffmpegCommand;

        /// <summary>
        /// 動画上の意味的な目印一覧です。
        /// </summary>
        public VideoRecordingMarker[] markers;
    }
}
#endif
