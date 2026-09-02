# UniLab.AI 設計書 — AI エージェント向け検証ツール群と動画録画

作成日: 2026-09-02
ステータス: 設計 + 実装
前提: `UniLab.Diagnostics`（FileLogSink / UiLayoutAuditor / SceneHierarchyDumper / UiScenarioRunner、#47 で実装済み）

---

## 目的

AI エージェントが**ゲームを実行し、自分で見て、何がおかしいかを判断して直す**ための基盤を整える。

現状の `UniLab.Diagnostics` は静止画（スクリーンショット）と JSON 監査までは撮れる。
しかし静止画では以下が判断できない。

- 遷移・フェード・アニメーション・トーストのような**時間のある挙動**
- 「撮影した瞬間」が偶然おかしかっただけなのか、継続的に壊れているのか

実際にフェード途中を撮影して UI バグと誤診した事故が起きている。動画にすれば撮影タイミングの
問題そのものが消え、AI が視覚的に原因を特定して自律的に修正できる。

あわせて、これら AI 向けツールを**いつでも別リポジトリへ切り出せる形**に再編する。

---

## 1. モジュール境界（別リポジトリ化の前提）

### 配置

AI 向けツールを `UniLab` 本体から物理的に切り離し、**UPM パッケージの形**で独立させる。

```
Assets/
  UniLab/          ← 従来のゲーム基盤（触らない）
  UniLab.AI/       ← 新設。この階層ごと別リポジトリへ移せる
    package.json         （name: com.unilab.ai）
    README.md
    Runtime/
      UniLab.AI.asmdef        （name: UniLab.AI）
      ...
    Editor/
      UniLab.AI.Editor.asmdef （name: UniLab.AI.Editor）
      ...
```

`Assets/UniLab/` の兄弟に置くのが要点である。配下に置くと本体の rsync 同期に巻き込まれ、
切り出しのたびにフォルダを掘り出す作業が要る。兄弟なら**フォルダを丸ごと移すだけ**で分離が完了する。

### 依存の鉄則

- **`UniLab.AI` は `UniLab` を参照しない。** 逆方向（UniLab → UniLab.AI）も禁止する
- 現状の `UniLab.Diagnostics` は `using UniLab.*` がゼロで、既にこの条件を満たしている。
  移設で新たな依存を持ち込まないこと
- R3 / UniTask / VContainer にも依存させない。**依存は UnityEngine・.NET 標準・`Unity.TextMeshPro` のみ**とする。
  TextMeshPro は UI 検査でテキスト要素を読むために必要で、Unity 標準パッケージのため切り出しの妨げにならない。
  切り出し先のリポジトリでパッケージ解決に悩まないことを優先する
- この制約のため、毎フレーム処理は `UpdateAsObservable` ではなく `MonoBehaviour` の
  `Update` / コルーチンで書く。UniLab 本体のライフサイクル規約から意図的に外れる箇所であり、
  理由は「依存ゼロの維持」である

### 名前空間

| 対象 | 名前空間 |
|---|---|
| 実行時 | `UniLab.AI` |
| エディタ拡張 | `UniLab.AI.Editor` |

既存の `UniLab.Diagnostics` / `UniLab.Diagnostics.Editor` は `UniLab.AI` / `UniLab.AI.Editor` へ改名して移設する。
`UniLab.Debug` が `UnityEngine.Debug` と衝突した前例があるため、**`Debug` という語をこの階層で使わない**。

### 切り出し手順（将来の作業）

1. `Assets/UniLab.AI/` を新リポジトリのルートへ移す
2. 利用側の `Packages/manifest.json` に git URL で追加する
3. 利用側から `Assets/UniLab.AI/` を削除する

コード変更は発生しない。この状態を維持することが本設計の制約である。

---

## 2. 動画録画（VideoRecorder）

### 方式の比較と採用

| 方式 | 長所 | 短所 | 判定 |
|---|---|---|---|
| **`ScreenCapture.CaptureScreenshotAsTexture` + 連番 JPG（採用）** | **合成後の最終画面が撮れる**。Overlay Canvas の UI も 3D も写る。同期実行でフレームの取りこぼしが無い | エンコードが重い | **採用** |
| `ScreenCapture.CaptureScreenshot(path)` を毎フレーム | 手軽 | 非同期完了のため同一フレーム内で上書き・取りこぼしが起きうる | 不採用 |
| Camera → RenderTexture → AsyncGPUReadback | 高速・低負荷 | **`ScreenSpaceOverlay` の UI が一切写らない** | **不採用** |
| Unity Recorder パッケージ | 公式・mp4 直出力 | 依存増・エディタ専用 | 不採用 |

**カメラ経由を採らない理由が決定的である。** 利用側（karakuri）の Canvas は全て
`RenderMode.ScreenSpaceOverlay` であり、Overlay はカメラのレンダーターゲットに描かれない。
UI の見た目検証が主目的である以上、カメラ経由は要件を満たさない。

採用方式は**合成後のバックバッファ**を読むため、3D ジオメトリ・スカイボックス・ポストプロセス・
あらゆる Canvas の描画モードが同時に写る。将来 3D 空間を含む画面へ移行しても方式変更は不要である。

### 時間の扱い

`Time.captureFramerate` に録画 FPS を設定する。Unity は「1フレーム = 1/fps 秒」として
時間を進めるため、エンコードで実時間が伸びてもコマ落ちの無い動画になる。

- 録画中は実時間より**ゲームが遅くなる**。これは仕様とする
- 停止時に必ず `Time.captureFramerate = 0` へ戻す。例外時も戻すこと

### 撮影タイミング

`CaptureScreenshotAsTexture` は**レンダリング完了後**に呼ぶ必要がある。
`Update` ではなくコルーチンで `WaitForEndOfFrame` を待ってから撮る。

### 保存形式

フレームは **JPG（品質 90）** で書き出す。

同一シーン（1397x786 の UI 画面・229 フレーム）での実測は以下のとおり。

| 形式 | 1フレーム平均 | 合計 |
|---|---|---|
| PNG | 126.6 KB | 29.0 MB |
| JPG 品質 90 | 103.1 KB | 23.1 MB |

**平坦な UI 画面では差が小さい（約 19% 減）。** PNG はベタ塗りを極めて良く圧縮するためで、
この条件だけを見れば JPG を選ぶ理由は薄い。

採用の根拠は**3D 画面**にある。PNG は可逆圧縮のため、テクスチャやライティングで
情報量が増えるほどフレームサイズが跳ね上がる。JPG は品質設定で上限が決まり、
内容によらず概ね一定に収まる。**将来 3D を含む画面を録る前提では JPG のほうが破綻しない。**

品質 90 を選ぶのは、検証で AI が**画面内のテキストを読む**ためである。
品質を落とすと文字周りの圧縮ノイズで可読性が落ちる。実測では品質 90 で
ピクセルフォントの小さな文字も潰れないことを目視確認した。

### mp4 への変換

**Unity 内で ffmpeg を起動しない。** ゲームコードにプロセス起動を持ち込まない方針を維持する。
録画結果に変換コマンド文字列を含めて返し、実行は Mac 側（検証を運転する AI / 人間）が行う。

```
ffmpeg -y -framerate 30 -i frame-%05d.jpg -c:v libx264 -pix_fmt yuv420p -vf "pad=ceil(iw/2)*2:ceil(ih/2)*2" out.mp4
```

`pad` フィルタは H.264 が偶数解像度を要求するため。奇数幅の画面で変換が失敗する事故を防ぐ。

---

## 3. AI が自律的に直すための仕掛け（本設計の中核）

**動画を撮るだけでは AI は直せない。** 「00:03 で表示が壊れている」と分かっても、
それがどの操作の結果で、そのときログに何が出ていたかが辿れなければ修正に繋がらない。

そこで録画と同時に **`recording-manifest.json`** を出力し、
**動画の時刻 ↔ シナリオのステップ ↔ ログ**を対応付ける。

```json
{
  "name": "battle_first_fight",
  "framesPerSecond": 30,
  "frameCount": 600,
  "width": 1397,
  "height": 786,
  "startedAtRealtime": "2026-09-02T10:30:00+09:00",
  "ffmpegCommand": "ffmpeg -y -framerate 30 -i frame-%05d.jpg ...",
  "markers": [
    { "frame": 0,   "timeSeconds": 0.0, "label": "step12 submit=RoomCard0" },
    { "frame": 150, "timeSeconds": 5.0, "label": "step13 capture=12_dungeon_battle" }
  ]
}
```

これにより AI は次の経路で原因に到達できる。

1. 動画を見て「5秒あたりが壊れている」と判断する
2. manifest の `markers` から該当フレームが `step13` だと引く
3. そのステップの静止画 `12_dungeon_battle.png` と監査 JSON、
   および `FileLogSink` のログの同時刻付近を突き合わせる
4. 原因のコードへ辿り着く

マーカーはシナリオランナーが各ステップ開始時に自動で打つ。手動運転（`DebugUiDriver`）からは
`AddMarker(label)` で任意に打てる。

---

## 4. API

```csharp
/// <summary>連番 JPG による画面録画。使い捨て GameObject として動く。</summary>
public sealed class VideoRecorder : MonoBehaviour
{
    /// <summary>録画を開始する。出力先ディレクトリは呼び出し側が決める。</summary>
    public static VideoRecorder StartRecording(string outputDirectory, string name, int framesPerSecond = 30);

    /// <summary>現在フレームに目印を打つ。動画の時刻とシナリオ上の意味を対応付けるために使う。</summary>
    public void AddMarker(string label);

    /// <summary>録画を停止し、フレーム数・出力先・ffmpeg コマンドを含む結果を返す。</summary>
    public VideoRecordingResult StopRecording();

    /// <summary>録画中かどうか。</summary>
    public bool IsRecording { get; }
}
```

出力先の既定は `DebugOutputPath.DirectoryPath/recordings/<name>/`。

---

## 5. シナリオ統合

`UiScenarioStep` に2フィールドを追加する。

| フィールド | 動作 |
|---|---|
| `recordStart: true` | このステップの開始時に録画を開始する |
| `recordStop: "<名前>"` | このステップの完了時に録画を停止し、`<名前>` で確定する |

```json
{ "submit": "RoomCard0", "recordStart": true },
{ "waitFrames": 600, "recordStop": "battle_first_fight" }
```

`UiScenarioRunner` は録画中、各ステップ開始時に自動でマーカーを打つ。
シナリオがタイムアウトや完了で終わるとき、録画中なら**必ず停止する**（`captureFramerate` の戻し漏れ防止）。

---

## 6. 出力レイアウト

```
DebugOutput/
  recordings/
    battle_first_fight/
      frame-00000.jpg
      frame-00001.jpg
      ...
      recording-manifest.json
```

`DebugOutput/` は利用側で gitignore 済みであること。連番 JPG も本数が増えれば容量を食うため、
変換後にフレームを消すかどうかは利用側の運用に委ねる（ツール側では消さない）。

---

## 7. 既知の制約

- **エディタが非フォーカスだと Game View が再描画されず、同じ絵が録れる。**
  `Application.runInBackground` を有効にすること（利用側の設定）
- 録画中は実時間が伸びる。長尺の録画には向かない。目安は1回30秒以内。
  **3D 画面ではエンコード負荷が上がるため、この目安はさらに短くなる**
- 連番 JPG は容量を食う。1397x786 の UI 画面で実測 約103KB/frame（30fps で約3MB/秒）。
  3D 画面ではこれより大きくなる
- 音声は録らない。UI・見た目の検証が目的であり、音声は対象外

## 8. スコープ外

- 音声の録音
- Unity エディタ画面そのものの録画（`docs/tasks/editor-capture-design.md` 側の主題）
- 動画の差分比較・自動判定（まず人間と AI が目で見る段階を作る）
