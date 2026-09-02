# UniLab.AI

AI エージェントが**ゲームを実行し、自分で見て、何がおかしいかを判断する**ためのツール群。

設計の詳細は `docs/design-unilab-ai-tools.md` を参照。

## 収録しているもの

| 種別 | クラス | 役割 |
|---|---|---|
| 記録 | `FileLogSink` | Unity ログを全てファイルへ複写する |
| 記録 | `VideoRecorder` | 画面を連番 JPG で実時間どおりに録画し、時刻とステップを対応付ける manifest を出す |
| 記録 | `AudioRecorder` | ミックス後の音声を WAV へ書き出す。動画と同じ時間軸なのでそのまま多重化できる |
| 観測 | `UiLayoutAuditor` | UI のはみ出し・重なりを検出して JSON で返す |
| 観測 | `SceneHierarchyDumper` | シーン階層と SerializeField の結線状態をテキストで出す |
| 運転 | `UiScenarioRunner` | JSON シナリオに従って UI を操作し、撮影・録画・監査を自動実行する。対象が押せる状態になった瞬間に操作し、フレーム数で待たない |

## この階層の制約（必ず守ること）

**このフォルダはいつでも別リポジトリへ切り出せる状態を保つ。** そのための制約が4つある。

1. **`UniLab` 本体を参照しない。** 逆方向（`UniLab` → `UniLab.AI`）も禁止する
2. **R3 / UniTask / VContainer に依存しない。** 依存は `UnityEngine`・.NET 標準・`Unity.TextMeshPro` に限る。
   TextMeshPro だけは UI 検査でテキスト要素を読むために要る。Unity 標準パッケージなので切り出しの妨げにならない
3. **毎フレーム処理は `UpdateAsObservable` を使わない。** R3 に依存しないため、`MonoBehaviour` の
   `Update` やコルーチンで書く。UniLab 本体のライフサイクル規約から意図的に外れている箇所である
4. **名前空間に `Debug` という語を使わない。** `UnityEngine.Debug` と衝突した前例がある

## 切り出す手順

1. `Assets/UniLab.AI/` を新しいリポジトリのルートへ移す
2. 利用側の `Packages/manifest.json` に git URL で追加する
3. 利用側から `Assets/UniLab.AI/` を削除する

コード変更は発生しない。

## 動画録画の使い方

録画は連番 JPG（品質 90）で出力し、mp4 への変換は**呼び出し側（Mac）が ffmpeg で行う**。
ゲームコードにプロセス起動を持ち込まないため。

シナリオから使う場合:

```json
{ "submit": "RoomCard0", "recordStart": true, "recordFps": 60, "recordAudio": true },
{ "settleFrames": 600, "recordStop": "battle_first_fight" }
```

`recordFps` を省略すると 30fps。**60fps でフレーム落ちなしを実測で確認済み。**
`recordAudio` の既定は false。指定すると `audio.wav` を出し、ffmpeg コマンドが多重化まで行う。

出力は `DebugOutput/recordings/<名前>/` に連番 JPG と `frames.txt`、`recording-manifest.json` が並ぶ。
manifest の `ffmpegCommand` をそのまま実行すれば mp4 になる。

**動画の尺は録画した実時間と一致する。** 7 秒録れば 7 秒の動画になる（実測誤差 1.35 ミリ秒）。
そのために `Time.captureFramerate` は使わず、`Application.targetFrameRate` で描画レートを絞り、
フレームごとの実時刻を `frames.txt` に持たせている。将来 音声を重ねられるようにするための土台でもある。

`markers` が動画の時刻とシナリオのステップを対応付ける。
「何秒で壊れているか」から「どのステップか」へ辿るための索引である。

## シナリオの待ち方

ランナーは**対象が「存在し・遮られておらず・操作可能」になった瞬間に送出する**。
フレーム数で待たないので、動画に写る間はゲーム本来の応答時間そのものになる。

- `settleFrames` は撮影・監査のあるステップだけ既定 30。それ以外は 0（操作後すぐ次へ）
- `waitScene` は操作した**結果**のシーン到着を待つ
- 押せる状態にならなければ 30 秒で見送り、警告を出す。モーダル越しに押すことはない
- マーカーの `waited` が「押せるまで待った実時間」＝ゲームの応答時間

## 既知の制約

- エディタが非フォーカスだと Game View が再描画されず、同じ絵が録れる。
  利用側で `Application.runInBackground` を有効にすること
- 録画中は描画レートを目標 fps へ絞るため、ゲームの動きが普段より遅く見えることがある。目安は1回30秒以内
- エンコードが目標 fps に間に合わない分は素直にフレーム落ちとして記録される。
  捨てた数は manifest の `droppedFrameCount` に出る。3D の高解像度・高 fps では影響が大きくなる
- **音声を録るにはシーンに `AudioListener` が要る。** 無いと Unity はミックスを生成せず、
  正しい長さの無音 WAV ができるだけで気づきにくい
