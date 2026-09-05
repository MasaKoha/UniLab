#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniLab.AI
{
    /// <summary>
    /// シナリオの 1 ステップ。指定されたフィールドだけが実行される。
    /// </summary>
    [Serializable]
    public sealed class UiScenarioStep
    {
        /// <summary>
        /// submit を送る GameObject 名。親名/子名 のパス指定にも対応する。空なら操作しない。
        /// </summary>
        public string submit;

        /// <summary>指定要素を祖先 ScrollRect の表示範囲へ入れる。フォーカスは動かさない。</summary>
        public string scrollTo;

        /// <summary>
        /// ボタン 1 発を次フレームで離し、プレイヤー入力と同じ経路へ落とすための語彙です。
        /// </summary>
        public string press;

        /// <summary>
        /// 長押しの始点ボタンを指定し、UI の長押し遷移を検証できるようにするための語彙です。
        /// </summary>
        public string hold;

        /// <summary>
        /// フォーカス移動を語として表し、D-Pad 依存 UI を簡潔に書くための語彙です。
        /// </summary>
        public string move;

        /// <summary>
        /// 左右どちらのスティックを使うかを明示し、同じ x/y でも意味を固定するための軸名です。
        /// </summary>
        public string stick;

        /// <summary>
        /// キーボード単打を語彙化し、パッド非対応の画面にも同じ仕組みで届くようにするためのキー名です。
        /// </summary>
        public string key;

        /// <summary>
        /// TMP_InputField へ文字列を流し込み、OS キーボードに依存せず文字入力を再現するための語彙です。
        /// </summary>
        public string text;

        /// <summary>
        /// 要素名を座標へ変換してポインタを動かし、クリック前の hover 解決を実機と同じ流れにするための対象です。
        /// </summary>
        public string pointerMove;

        /// <summary>
        /// 要素名クリックを短く書けるようにしつつ、内部では座標クリックへ正規化するための対象です。
        /// </summary>
        public string click;

        /// <summary>
        /// drag を明示語彙として分け、座標ペアだけでは通常移動と混同しないようにするための対象です。
        /// </summary>
        public string drag;

        /// <summary>
        /// scroll を明示語彙として分け、量指定のあるポインタ操作を曖昧にしないための対象です。
        /// </summary>
        public string scroll;

        /// <summary>
        /// 要素名タップを短く書けるようにしつつ、内部では仮想 Touchscreen へ正規化するための対象です。
        /// </summary>
        public string tap;

        /// <summary>
        /// swipe を明示語彙として分け、マウス drag とタッチ swipe を別経路で再現するための対象です。
        /// </summary>
        public string swipe;

        /// <summary>
        /// pinch を明示語彙として分け、2 指操作の中心指定を他の座標語彙から分離するための対象です。
        /// </summary>
        public string pinch;

        /// <summary>
        /// 始点名を要素基準で書けるようにし、ドラッグやスワイプの JSON を可読に保つための始点です。
        /// </summary>
        public string from;

        /// <summary>
        /// 終点名を要素基準で書けるようにし、ドラッグやスワイプの JSON を可読に保つための終点です。
        /// </summary>
        public string to;

        /// <summary>
        /// ピンチ中心を要素名で表せるようにし、ズーム対象との対応を崩さないための中心です。
        /// </summary>
        public string center;

        /// <summary>
        /// click のボタン種別を文字列で受け、JSON 側を人間が書きやすい語彙に保つための指定です。
        /// </summary>
        public string button;

        /// <summary>
        /// ボタン長押しや stick/drag/swipe/pinch の継続時間を共有し、余計なフィールド増殖を避けるための秒数です。
        /// </summary>
        public float seconds;

        /// <summary>
        /// 座標入力や stick ベクトルの X 成分を共有し、JsonUtility の単純なフィールド構成へ収めるための値です。
        /// </summary>
        public float x;

        /// <summary>
        /// 座標入力や stick ベクトルの Y 成分を共有し、JsonUtility の単純なフィールド構成へ収めるための値です。
        /// </summary>
        public float y;

        /// <summary>
        /// drag / swipe の始点座標を要素名無しでも指定できるようにするための値です。
        /// </summary>
        public float fromX;

        /// <summary>
        /// drag / swipe の始点座標を要素名無しでも指定できるようにするための値です。
        /// </summary>
        public float fromY;

        /// <summary>
        /// drag / swipe の終点座標を要素名無しでも指定できるようにするための値です。
        /// </summary>
        public float toX;

        /// <summary>
        /// drag / swipe の終点座標を要素名無しでも指定できるようにするための値です。
        /// </summary>
        public float toY;

        /// <summary>
        /// scroll 量や pinch 距離のような単独スカラーを再利用し、語彙ごとの専用型乱立を避けるための値です。
        /// </summary>
        public float amount;

        /// <summary>
        /// ピンチ開始距離を明示し、録画なしでもズーム方向を JSON から読めるようにするための値です。
        /// </summary>
        public float fromDistance;

        /// <summary>
        /// ピンチ終了距離を明示し、録画なしでもズーム方向を JSON から読めるようにするための値です。
        /// </summary>
        public float toDistance;

        /// <summary>
        /// テキスト待機を明示し、生入力でも対象が無いケースの同期点を作るための条件です。
        /// </summary>
        public string waitForText;

        /// <summary>
        /// 要素出現待機を明示し、ポインタや submit の前に押せる状態を待つための条件です。
        /// </summary>
        public string waitForObject;

        /// <summary>
        /// フォーカス待機を明示し、方向入力ベース UI の同期点を取るための条件です。
        /// </summary>
        public string waitForFocus;

        /// <summary>
        /// シーン待機を明示し、ロードタイミングが画面ごとに揺れるケースを吸収するための条件です。
        /// </summary>
        public string waitForScene;

        /// <summary>
        /// このシーン名がロード済みになるまで待つ。空なら待機しない。
        /// </summary>
        public string waitScene;

        /// <summary>
        /// 撮影ファイル名。拡張子は付けない。空なら撮影しない。
        /// </summary>
        public string capture;

        /// <summary>
        /// 01 の構造スナップショット名です。画像ではなく JSON を保存したいステップで使います。
        /// </summary>
        public string snapshot;

        /// <summary>
        /// true のとき UiLayoutAuditor を実行し JSON を保存する。
        /// </summary>
        public bool audit;

        /// <summary>
        /// true のとき、このステップの開始時に録画を開始する。
        /// </summary>
        public bool recordStart;

        /// <summary>録画のフレームレート。0 以下のとき既定値（30）を使う。recordStart と同じステップに書く。</summary>
        public int recordFps;

        /// <summary>true のとき録画に音声を含める。recordStart と同じステップに書く。</summary>
        public bool recordAudio;

        /// <summary>
        /// 空でないとき、このステップの完了時に録画を停止し、この名前で確定する。
        /// </summary>
        public string recordStop;

        /// <summary>
        /// 整定待ちフレーム数。0 以下のとき既定値を使う。
        /// </summary>
        public int settleFrames;

        /// <summary>
        /// 操作後に観測で判定する期待値です。
        /// </summary>
        public ScenarioExpectation[] expect;

        /// <summary>
        /// シナリオ既定をステップ単位で上書きするための入力可視化指定です。
        /// </summary>
        public bool inputOverlay;

        /// <summary>
        /// JsonUtility の bool 未指定問題を補い、ステップ false 上書きを録画設定へ渡すための内部情報です。
        /// </summary>
        [NonSerialized]
        public bool inputOverlaySpecified;

        /// <summary>
        /// 1 ステップだけランダム探索へ委譲するための指定です。
        /// </summary>
        public MonkeyOptions monkey;
    }
}
#endif
