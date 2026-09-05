# 12 AI ゲートウェイ（PR1）

## 目的

CLI と Unity 内蔵メールボックスの実行先を `AiCommandDispatcher` に統一する。
`AgentSessionCommands` から既存セッション API を呼び、`AgentSession` 本体と
`AiSessionState.Enter/Exit` は変更しない。外部 Python 中継プロセスは不要になる。

この PR は `Assets/UniLab.AI/` の外を変更しないため、設計書・roadmap 追記・EditMode テストも
この階層内に置く。テストアセンブリ名は `UniLab.AI.Tests.EditMode`。

## 操作一覧

`AiCommandRequest` は `op` と `args` を持つ。`args` は JSON オブジェクトを格納した**文字列**で、
省略・空文字列は `{}` として扱う。Python クライアントはこの二重の JSON 化を引き受ける。

| op | args | 結果 |
|---|---|---|
| `ping` | なし | `playMode=<bool> scene=<name> frame=<n>` |
| `ops` | なし | 登録済み op を改行区切りで返す |
| `agent.begin` | `goal` 必須、`options` 任意 | freePlay:true は期待値 0 件を許可。それ以外は拒否 |
| `agent.observe` | `diffOnly=false`、`scope="visible"`、`capture`・`directory` 任意 | 現在の観測。撮影指定時は画像情報も返す |
| `agent.find` | `label`、`kind` 任意、`scope="visible"` | 観測を検索し、一件一行で推奨 target spec を返す |
| `agent.act` | `action` または空でない `steps` 配列、`expect` 任意 | 各手を順に実行し、expect 未達・status が running 以外なら打ち切る |
| `agent.goal` | なし | 既存の目標判定 |
| `agent.end` | なし | 既存のセッション終了 |
| `agent.export` | `name` | 成功または自由行動セッションのシナリオ保存 |
| `capture` | 英数字・`_`・`-` だけの `name` 必須、`directory` 任意 | PNG の絶対パス。既定は `DebugOutput/captures` |
| `snapshot` | `compact=true`、CLI 互換の `save=false` | 圧縮テキストまたはスナップショット JSON を text に格納 |
| `console` | `count=40`、`level="all"` | 直近 500 行のリングから対象レベルの末尾 N 行 |

`AiCommandResponse` は `ok/op/session/message/text/path/width/height/blank/settled/ready/expectOk/expectFailures/waitedMs/elapsedMs/error` を持つ。
既存の `AgentCommandResult` の `ok/session/message/text/path` は同名・同型を保つ。
変換はディスパッチャの一箇所に集約する。未知 op は `error:"unknown op"`、不正 JSON は
`ok:false` と例外メッセージを返す。`agent.*` は Play 外で `message:"playMode が必要です"`。

## 同期経路と落ち着き待ち

CLI は `Execute` を使う。単発 act は従来どおり即時の観測を返し、`settled=false`。
同期 capture は要求と予定パスを返すだけで、撮影完了を保証しない。

メールボックスは `ExecuteAsync` を使う。各 act の直前から `sceneLoaded` を購読し、
入力後に最低一度フレームを進める。`AgentSessionDriver.IsBusy` と列挙可能な未ロードシーンを監視し、
それらがなくなってから実時間 `settleSeconds`（既定 0.35 秒）を待つ。
シーン到着イベントが来たら静止時間を計り直す。各手の全待機には
`settleTimeoutSeconds`（既定 10 秒）の上限があり、`Time.realtimeSinceStartup` で計測する。
`Time.timeScale=0` でも待機上限は進む。

待機完了後に Observe を取り直し、行動結果の status / message 行と最新の観測を返す。
成功した待機は `settled=true`。上限超過は `ok:false, settled:false, error:"settle timeout"` とし、
その時点の観測を返して後続手を送らない。目標判定・手数・拒否・記録は既存セッションに任せる。

非同期 capture は同名の前回画像を削除して撮影を要求し、ファイルの生成を実時間最大 3 秒待つ。
生成確認時は `settled=true`、未生成なら `error:"capture timeout"`。

## メールボックスのプロトコル

既定ディレクトリは `<Application.dataPath の親>/DebugOutput/agent-mailbox`。

1. クライアントは一意な ID（Python は UUID）で `req-<id>.json.tmp` を書いて閉じる。
2. 同じディレクトリ内で `req-<id>.json` に rename して公開する。
3. サーバーは `Update` から既定 0.05 秒ごとに `req-*.json` を名前順に走査する。
4. 同時に一件だけ実行する。応答が書き終わるまでは次の要求を実行しない。
5. `res-<id>.json.tmp` を書き、`File.Move` で `res-<id>.json` を公開する。
6. 応答公開後に要求を削除する。応答はクライアントが読むため残す。

`.tmp` は列挙対象外で、読み取りヘルパに直接渡しても拒否する。
応答書き込み失敗時は結果を保持して書き込みだけを再試行し、操作を再実行しない。
応答公開後・要求削除前に停止した場合、次の走査は既存応答を認識して要求だけを削除する。
起動時に更新日時が 1 時間より古い `res-*.json` を削除する。

Stop はコルーチンとシーン購読を解放し、処理中の要求へ `ok:false, error:"server stopped"` を
公開してからサーバーを破棄する。セッション自体は終了しない。
ファイル書き込みが失敗した場合、明示 Stop は例外を返しサーバーを維持する。
Unity 終了中の I/O 失敗やプロセスクラッシュでは応答を保証できない。

## 起動方法

すべて `AiMailboxServer.Start(directory)` に集約する。

- **マーカー**: 既定メールボックスに `.enabled` を作り、Play を開始する。
  `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` が自動起動する。
- **Editor**: Play 中に `UniLab/AI/Mailbox/Start` / `Stop`。
- **CLI**: `ai_mailbox --start` / `--stop` / `--status`。
  開始時は `--directory <dir>` も指定できる。

状態は `AiMailboxServer.IsRunning` / `Directory` / `HandledCount`。
サーバーは Resources の設定アセットが保持する型付き Prefab から生成し、シーンをまたいで存続する。
Stop は `.enabled` を削除しないため、次の Play では再び自動起動する。

## クライアント

```sh
python3 Assets/UniLab.AI/Tools/ai_client.py ping
python3 Assets/UniLab.AI/Tools/ai_client.py agent.begin '{"goal":{"freePlay":true,"maxSteps":5000,"maxSeconds":14400}}'
python3 Assets/UniLab.AI/Tools/ai_client.py agent.act '{"action":{"submit":"NewGameButton"}}'
python3 Assets/UniLab.AI/Tools/ai_client.py agent.act '{"steps":[{"press":"east"},{"submit":"TabButton1"}],"settleSeconds":0.35}'
python3 Assets/UniLab.AI/Tools/ai_client.py capture '{"name":"03_workshop"}'
```

パスは `--mailbox DIR` → `UNILAB_AI_MAILBOX` → カレントから親へ既存ディレクトリを探索、の順。
初回だけは `Assets` と `ProjectSettings` のある親を見つけて既定ディレクトリを作る。
`.enabled` は自動作成する。待ち時間は `--timeout`（既定 60 秒）。
応答の text 以外を先頭の一行 JSON、text を続く本文に表示する。成功の終了コードは 0、失敗は 1。
タイムアウト後も要求を残すため、その要求が後から実行される可能性がある。再送は別要求になる。

## CLI との使い分けと互換性

即時操作・既存スクリプトには CLI、フレームをまたぐ操作や localhost に届かない環境にはメールボックスを使う。
追加 CLI は `ai_capture` / `ai_mailbox` / `ai_ops`。
既存 agent CLI のコマンド名と引数名を保ち、応答 JSON には既存キーを残す。

`ai_snapshot` の `compact` / `save` 引数と保存動作は保持するが、今回の応答統一指定に従い、
従来の生テキスト／オブジェクトを `AiCommandResponse` の JSON 文字列へ変更する。
このコマンドの旧戻り値を直接読むクライアントは `text` を読む必要がある。
Play 外の agent CLI も従来の生文字列から、同じ文言を含む応答 JSON になる。

## 既知の制約

- Runtime は Editor / Development Build に限定する。Pipeline は `UNILAB_AI_PIPELINE` が必要。
- メールボックスの起動と agent 操作には PlayMode が必要。Play 中にマーカーを置くだけでは起動せず、
  メニューまたは CLI で開始するか、マーカーを置いた状態で次の Play を開始する。
- 独自ディレクトリの `.enabled` は自動探索しない。メニューは既定パス、CLI / API は任意パスを扱う。
- 同時処理は一要求。CLI とメールボックスを同じセッションへ同時に送る排他制御は PR1 の対象外。
- Unity には任意の `LoadSceneAsync` の開始を全体で通知するイベントがない。
  シーン一覧に未登場の AsyncOperation や `allowSceneActivation=false` は検出できない。
  ロード完了イベントは静止待ちを延長するが、未公開のロードを完全に待つにはゲーム側の通知が別途必要。
- 落ち着き待ちはアニメーションの完了そのものを検出しない。長い遷移には settleSeconds を調整する。
- 非同期処理後の目標達成をセッションが確定するタイミングは既存実装のまま。
- 撮影完了判定はファイルの存在であり、画像デコード・最終書き込み完了までは保証しない。
- ファイルログ読み取りは末尾 N 行だけを保持するが、ファイルは先頭から走査する。
- クラッシュをまたぐ exactly-once は保証しない。実行後・応答公開前のクラッシュで要求が残り得る。
- Unity の起動・コンパイル・EditMode テストは依頼者が実施する。


## 準備待ち

メールボックスの `agent.act` は `submit` / `click` / `tap` の対象について、
`UiReadiness.IsSubmittable` で存在・遮蔽なし・操作可能を確認してから既存の `Act` を呼ぶ。
ランナーも同じヘルパを使い、シナリオのアンカー条件は引き続きランナー側で判定する。
`readyTimeoutSeconds` は実時間で既定 5 秒。0 は即時判定、負値・NaN・無限大は拒否する。
上限到達時も `Act` を呼び、submit の対象なし・遮蔽・操作不可などの既存メッセージを保持する。
click / tap の対象解決失敗時は従来の座標フォールバックも保持する。
準備待ちがタイムアウトした手では落ち着き待ちと観測更新を省き、追加フィールド以外は Act の応答をそのまま返す。steps もそこで打ち切る。
`steps` は各手について準備待ち → 実行 → 落ち着き待ちの順に処理する。
同期 CLI は準備待ちをせず即時実行する。

## 観測の可視フィルタ（offscreen / clipped / scope）

要素矩形は画面座標の `[x, y, width, height]`。`ComputeVisibleRatio` は交差面積を
要素面積で割った 0〜1 を返し、要素面積が 0 の場合は 0 とする。

- `offscreen`: 画面矩形との交差が要素面積の 10% 未満。
- `clipped`: 最寄りの有効な祖先 `RectMask2D` または Image 付き `Mask` の矩形との交差が 50% 未満。祖先マスクがなければ false。
- `agent.observe` の `scope:"visible"`（既定）は両方を除外する。`scope:"all"` は画面外も含め、clipped 行の末尾に ` [clipped]` を付ける。不正な scope は `ok:false`。
- 通常の `UiSnapshot.ToCompactText` は offscreen を除き、clipped を注記付きで残す。`snapshot` op は全要素を返し、保存 JSON も全要素を保持する。
- `actions:` は scope に関係なく clipped / offscreen を除外する。
- `UiSnapshot.Compare` は clipped / offscreen の変化を changed に記録する。visible の差分観測では表示範囲に入った要素を added、外れた要素を removed として返す。

祖先探索は観測時だけ実施する。Selectable の表示ラベルは 80 文字、Text は 120 文字。
`label:` 推奨表記の長さは変更しない。

## 応答フィールド（ready / waitedMs / elapsedMs）

| フィールド | 型 | 意味 |
|---|---|---|
| `ready` | bool | 非同期経路で最終行動の対象が押下準備条件を満たしたか。タイムアウト・待機対象外・同期経路は false。入力ハンドラーでの成功とは別の判定 |
| `waitedMs` | int | 最終行動の準備待ち実時間（ミリ秒）。タイムアウト時も計測。待機対象外・同期経路は 0 |
| `elapsedMs` | int | ディスパッチャの実行開始から応答完成までの実時間（ミリ秒）。準備待ちと落ち着き待ちを含み、同期・非同期・失敗応答すべてで計測 |

`steps` の ready / waitedMs は既存の応答と同じく最後に実行した手の値。
elapsedMs は要求全体の値。ミリ秒未満は切り捨てるため即時応答は 0 になり得る。
メールボックスのキュー待ちや応答ファイル公開の時間は含まない。

## PR4: 観測品質とクライアント操作

### フリープレイ

`agent.begin {"goal":{"freePlay":true,"maxSteps":5000,"maxSeconds":14400}}` で、
期待値を持たないプレイテストを開始する。`freePlay` の既定は false で、通常目標では従来どおり
期待値が 1 件以上必要。検証は `AgentGoalValidator` に分離し、`Begin` では Play 判定より先に実施する。

自由行動では目標は常に未達で、レポートの `goalReached` も false。目標判定・反復検出による
自動終了を行わず、既存の手数・時間予算を行動時に確認する。禁止語の拒否と明示終了は維持する。
観測の `goalFailures:` 節は出さず、`agent.export` は目標未達の拒否をスキップし、
最後のステップに目標の `expect` やシーン待ちを追加しない。

### 観測＋撮影の同一フレーム

`agent.observe` に任意の `capture`（英数字・`_`・`-` の撮影名）を追加する。
スナップショット取得から `ScreenCapture.CaptureScreenshot` の発行まで yield を挟まず、
別要求によるフレームのずれを解消する。Unity の撮影自体はそのフレームの描画終了時であり、
フレーム内で後続処理が UI を更新した場合まで状態を凍結するものではない。
`text` は観測テキストを保持し、撮影先は `path` に返す。`directory` は単独撮影と同じ保存先指定。

単独の `capture` op も残し、両経路で `AiCaptureSupport` の撮影発行と完了待ちを共有する。
非同期版は最大 3 秒待って PNG を読み取り、応答に `int width` / `int height` / `bool blank` を設定する。
読み取りは `File.ReadAllBytes` → `Texture2D` → `ImageConversion.LoadImage` → `GetPixels32`。
RGB を 0〜255 の輝度（係数 0.2126 / 0.7152 / 0.0722）に変換し、母標準偏差が 3.0 未満なら
`blank=true`。白色に限定せず、ほぼ単色の画像を判定する。Texture は Play 中なら Destroy、
それ以外は DestroyImmediate で必ず破棄する。撮影時だけの処理で毎フレーム解析はしない。
同期 CLI は生成を待たず、`width=height=0` / `blank=false` のまま返す。

### console のリングバッファ

ファイルログ参照を廃止し、`Application.logMessageReceived` で収集した 500 行のリングを正とする。
`[{type}] {condition}` を格納し、Error / Exception / Assert にはスタック先頭 3 行を続ける。
各行のレベルを保持するため、容量超過で本文が落ちてもスタック行のエラー分類は維持される。
超過した最古行は Dequeue する。

`console {"level":"all","count":40}` が既定。`level:"error"` は Error / Exception / Assert と
そのスタック行だけに絞り、`count` は絞り込み後の末尾行数。無効な level・負の count は拒否する。
生成・購読は SubsystemRegistration のみで実行し、前回購読を解除して Play 再開時にリセットする。
静的コンストラクタでは生成せず、Play 開始前の未購読状態は空文字を返す。

### Text の遮蔽判定

Text の矩形中心にレイキャストし、GraphicRaycaster の結果から観測用 OverlayMarker 配下を除いた
最前面 Graphic を調べる。その Graphic が Text 自身・祖先・子孫のいずれでもなければ
`blockedBy` に遮蔽元の名前を格納する。Text 自身の `raycastTarget=false` でも判定できる。
これは中心点とレイキャスト対象 Graphic に基づく判定であり、文字の全ピクセルの遮蔽率ではない。

`visible` は遮蔽された Text を除外する。`all` は残して `blocked:<name>` を出力する。
Selectable は既存の遮蔽判定を維持し、visible でも押せない理由を表示する。


## PR5: 検索・事後条件・スクロールと省電力化

### agent.find

`agent.find {"label":"開始","kind":"Button","scope":"visible"}` は `UiSnapshot.Capture()` の
結果に `UiObservationScope.Filter` を適用して検索する。セッション開始は不要、PlayMode は必要。
`label` はリッチテキストタグ除去後の部分一致（大文字小文字を区別）。省略すると全ラベル。
`kind` は `Button` / `Text` / `Toggle` / `Input` / `Selectable`、省略時は全種別。
`scope` は `visible` が既定、`all` はマスク外・画面外も含める。

応答 `text` は次の一件一行形式。改行や引用符はエスケープする。

```text
Button Canvas/List/Row label="冒険を開始" interactable=true blockedBy="" clipped=false rect=[10,20,30,40] → submit:"label:冒険を開始"
```

推奨 spec は通常はパス。全観測内に同名要素がある場合は
`UiInputLocator.CreateLabelTargetSpec` による `label:` 指定を使う。ラベル自体も重複する場合は
既存 Locator の優先順位に従う。0 件は `ok:true`、`text:""`、`message:"見つかりません"`。

### agent.act の expect

```json
{"action":{"submit":"StartButton"},"expect":[{"kind":"sceneIs","value":"Game"}]}
```

単一行動は引数直下または `action.expect` に `ScenarioExpectation[]` を指定できる。
両方指定した場合は `action.expect` を優先する。`steps` は各行動オブジェクト内に指定する。

```json
{"steps":[{"scrollTo":"label:ステージ5"},{"submit":"label:ステージ5","expect":[{"kind":"textVisible","value":"準備完了"}]}]}
```

非同期経路は落ち着き待ち後に `AgentExpectationEvaluator.Evaluate` で評価する。
`textVisible` / `sceneIs` / `focused` など既存評価器の語彙・判定規則を使用し、`changed` は行動前後の差分を渡す。
応答に `expectOk:bool` と `expectFailures:string[]` を追加する。未指定は `true` と空配列。
未達理由は ` - kind target=... value=... message=...` の goalFailures と同じ一行形式。
事後条件未達だけでは `ok` を false にしない。一括実行は未達の手で停止し、
`message:"expect 未達で打ち切り"` とその手の結果を返す。
準備待ち・落ち着き待ちの既存タイムアウト契約は維持する。
同期経路はフレームを進めず、その時点の観測で評価するため `settled:false`。
行動後の画面変化を検証するクライアントはメールボックスの非同期経路を使う。

### scrollTo

`AgentAction.scrollTo` と `UiScenarioStep.scrollTo` は同じ対象指定を使い、
`UiInputLocator.FindTarget` で対象を解決する。祖先 ScrollRect を内側から順に扱い、
対象矩形が viewport に収まる最小移動を、座標系変換して `content.anchoredPosition` へ反映する。
有効な縦横軸だけを動かし、慣性を停止する。フォーカスは変更せず、Input System に依存しない。
対象が viewport より大きい軸は表示範囲を覆う位置まで最小移動し、既に覆っていれば動かさない。
ScrollRect が無い場合は「ScrollRect がありません」。準備待ちは対象の存在だけで判定する。
ログ種別は `scrollTo`、対象は指定文字列。シナリオへは `scrollTo` を保持して出力し、
`scroll` へ変換しない。各手の `expect` も保持し、最終手にはセッション目標を追加する。
入力候補には入力モードに関係なく ` - scrollTo=<target>` を表示する。

### メールボックスの省電力ポーリング

通常は `_pollIntervalSeconds=0.05` 秒。直近の応答処理完了から `_idleAfterSeconds=5` 秒以上
要求を処理していなければ `_idlePollIntervalSeconds=0.25` 秒へ伸ばす。起動直後は起動時刻を基準とする。
Prefab の SerializeField で調整できる。間隔は `ResolvePollInterval(lastHandledAt, now)` の純関数で解決し、
要求を一件処理した時点で最終処理時刻と次回ポーリング予定を更新して通常間隔に戻す。
`ai_mailbox --status` は `pollIntervalSeconds` と UTC ISO 8601 形式の `lastHandledAt` を追加する。
停止中の間隔は 0、未処理または停止中の最終処理時刻は空文字列。

### フレーム内のスナップショット共有

Play 中の `UiSnapshot.Capture()` は同じ `Time.frameCount` では同じ `UiSnapshotDocument` 参照を返す。
フレームが変わると再収集し、SubsystemRegistration でキャッシュをリセットする。
Play 停止中は毎回収集する。内部の `Capture(int frameCount)` で共有と更新を EditMode テストできる。
返された共有ドキュメントは変更せずに利用する。フレーム内の入力直後も最初の観測が返るため、
入力結果の再観測には次フレーム以降を使う。

### 追加テスト

- `AgentFindTest`: タグ除去・部分一致・種別・同名行の推奨 spec・0 件・scope・不正 kind。
- `AgentExpectTest`: 応答 JSON、未指定時の既定値、二手目未達で停止、単一行動の引数、changed 差分。
- `AiMailboxServerPollingTest`: 待機閾値、処理後の復帰、設定間隔の適用。
- `UiSnapshotCacheTest`: 同一参照、フレーム更新、Play 停止中のキャッシュ無効。
- `UiScrollToTest`: 最小移動とシナリオ語彙。`AgentActionExecutorTest` に scrollTo の種別・対象判定を追加。

## PR6: ゲーム側の busy 判定（`IGameBusyProvider`）

落ち着き待ち（`AiSettleWait`）はシーンロードと継続入力しか見ないため、フェード遷移や演出中の入力ブロック中でも `settled=true` を返すことがあった（Codex の所感「settled でもフェード中の応答があった」）。

- `GameAdapterRegistry.BusyProvider` にゲーム側が `IGameBusyProvider`（`IsBusy` / `Reason`）を登録する。未登録なら従来どおり
- `AiSettleWait` は busy の間を「静止していない」と扱い、静止時間の計測をやり直す（上限 `settleTimeoutSeconds` は従来どおり）
- 観測テキストに `agent: busy=<reason>` を出す。AI はこの行があれば途中経過として扱い、再観測する
- karakuri では `IInputBlockManager.BlockedInput`（ローディング・演出中の入力ブロック）を busy として登録する

## PR7: export の expect 化と scenario.run

`agent.act` に渡した `expect` は、単一 action・引数直下・steps のどの形式でも
各手の `UiScenarioStep.expect` へそのまま保存する。PR5 の `AgentActExpectation` は
配列を保管する型ではなく評価器なので、既存の `AgentAction.expect` のコピーを維持し、
評価後の `expectOk` を今回記録されたステップへ戻す。
未達だった手も削除せず、`comment: "元の実行では未達"` を付ける。
拒否されて記録が増えなかった手の評価は、直前のステップへ反映しない。

`agent.export` は `path` に scenario.json の絶対パス、`text` に
`steps=<全ステップ数> expectSteps=<expect を持つステップ数>` を返す。
freePlay の書き出しは従来どおり目標達成不要で、最終手へ目標条件を追加しない。
目標付きセッションの達成チェックと最終手への目標追加は従来どおり。

| op | 引数 | 応答 |
|---|---|---|
| `scenario.run` | `path` 必須（プロジェクト相対または絶対）、`name` 任意、`scenarioTimeoutSeconds` 既定 900 秒 | `path` は結果 JSON の絶対パス。同期は `status: "running"`、非同期は完了まで待ち `status: "completed"` と `verdict` を返す |
| `scenario.status` | なし | 直前に開始した結果の `path`、`status`、`verdict`、`failedSteps`、`warningCount` |

完了時の `scenario.run` も `failedSteps` と `warningCount` を返す。
`ok` はコマンド処理の成否で、回帰結果が `verdict: "fail"` でも `ok: true`。
完了前の `verdict` は空文字列。結果未作成・書き込み途中は `running` とする。
タイムアウトは `ok: false` / `error: "scenario timeout"` と予定 `path` を返す。
ランナーは停止せず、後から `scenario.status` で完了結果を回収できる。
待機先は起動した要求固有のパスで固定し、同名の連続実行でも前回結果と衝突させない。

`AiCommandDispatcher` が直前の結果パスを所有し、`AiScenarioExecution` が
既存 `UiScenarioRunner` の起動・結果読取・専用タイムアウトでの待機を共有する。
`ai_scenario_run` / `ai_scenario_status` もディスパッチャ経由とし、CLI の従来の返却形式
（開始時は予定パス、status は `resultFilePath` を持つオブジェクト）は維持する。
`settleTimeoutSeconds` はシナリオ全体の待機には使わない。
メールボックスは一要求ずつ処理するため、run 待機中の status 要求はその後に処理される。

```sh
python3 Assets/UniLab.AI/Tools/ai_client.py agent.export '{"name":"regression"}'
python3 Assets/UniLab.AI/Tools/ai_client.py scenario.run '{"path":"<export 応答の path>","name":"regression","scenarioTimeoutSeconds":900}' --timeout 930
```

クライアント側の `--timeout` はサーバーの `scenarioTimeoutSeconds` より長く設定する。
追加テストは `AgentExportTest`、`AiScenarioExecutionTest`、`InputOverlayInputStateTest`、
`AiCommandDispatcherTest` のシナリオ操作契約。Unity のコンパイル・再生・録画確認は別途行う。
