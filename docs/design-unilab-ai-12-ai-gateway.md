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
| `agent.begin` | `goal` 必須、`options` 任意 | 既存 Begin。期待値 0 件は既存文言で拒否 |
| `agent.observe` | `diffOnly=false` | 現在の観測 |
| `agent.act` | `action` または空でない `steps` 配列 | 各手を順に実行し、status が running 以外なら打ち切る |
| `agent.goal` | なし | 既存の目標判定 |
| `agent.end` | なし | 既存のセッション終了 |
| `agent.export` | `name` | 成功セッションのシナリオ保存 |
| `capture` | 英数字・`_`・`-` だけの `name` 必須、`directory` 任意 | PNG の絶対パス。既定は `DebugOutput/captures` |
| `snapshot` | `compact=true`、CLI 互換の `save=false` | 圧縮テキストまたはスナップショット JSON を text に格納 |
| `console` | `count=40` | 最後に初期化された稼働中 FileLogSink の末尾 N 行。未稼働なら直近 200 行のリングバッファ |

`AiCommandResponse` は `ok/op/session/message/text/path/settled/error` を持つ。
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
python3 Assets/UniLab.AI/Tools/ai_client.py agent.begin '{"goal":{"goal":[{"kind":"textVisible","value":"__never__"}],"maxSteps":5000,"maxSeconds":14400}}'
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
