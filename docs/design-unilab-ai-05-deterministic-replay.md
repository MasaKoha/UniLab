# 05 決定的リプレイ（シード固定＋入力記録・再生） 設計書

ステータス: 設計。ロードマップ M3
依存: 04 入力ボキャブラリ

---

## 目的

**バグを何度でも同じように再現する。** AI が自律的に直すには「再現 → 修正 → 再生 → 直ったことを確認」の
ループが必要で、再現が確率的だとこのループは閉じない。

再現性を壊す要因は2つ。乱数と、入力のタイミング。両方を固定する。

---

## 1. 乱数のシード固定

### 現状

karakuri は `System.Random` をコンストラクタ注入している（`BattleEngine(Random, ...)`、
`DungeonProgressor`、`SubstrateAptitudeGenerator`、`RuneAffixGenerator.Roll(..., Random)`）。
注入の口はあるが、**生成箇所が分散**しており、どこで `new Random()` されているかを一箇所で制御できない。

### 設計

UniLab.AI は乱数を扱わない。**シード固定はゲーム側の責務**とし、`IGameCommandHandler` の `seed` コマンドで受ける。

```
TryExecute("seed", { "value": "12345" })
```

karakuri 側の推奨実装:

- `Infrastructure` に `GameRandomSource` を1つ置き、`Random` を必要とする全クラスがここから受け取る
- `seed` コマンドで `GameRandomSource.Reset(seed)` を呼び、以降の `Random` 生成を決定的にする
- **シーン遷移やセーブロードをまたいでも同じ系列**になるよう、シードと「消費回数」をセーブ外の実行時状態として持つ

既に `BattleEngine` はシードから検算表を再現できる（STATUS）。この方式を全体に広げる。

### 記録

リプレイの記録には**シードと開始状態**を含める。開始状態はセーブデータのスナップショット
（karakuri の `Export Save As Json` チートが既にある）。

---

## 2. 入力の記録と再生

### 記録

04 の仮想デバイスへ注入した入力と、**実機からの入力**の両方を記録する。
実機入力は `InputSystem.onEvent` で全デバイスのイベントを購読して拾う。

`DebugOutput/replays/<名前>/inputs.jsonl`（1行1イベント）

```json
{"frame":120,"time":4.0033,"device":"Gamepad","control":"buttonSouth","value":1}
{"frame":121,"time":4.0367,"device":"Gamepad","control":"buttonSouth","value":0}
{"frame":180,"time":6.0012,"device":"Gamepad","control":"dpad/down","value":1}
```

`frame` は録画開始からの**ゲームフレーム番号**。再生はこのフレーム番号で合わせる。

### 再生

同じフレーム番号で `QueueStateEvent` を投げる。

**ここだけ `Time.captureFramerate` を使う。** 録画（実時間へ揃える）とは逆の判断であり、理由を明記する。

| 用途 | 時間の扱い | 理由 |
|---|---|---|
| 録画（検証・視聴・音声） | 実時間。`targetFrameRate` で絞る | 見え方と音声の同期のため |
| **リプレイ（再現）** | **固定ステップ。`captureFramerate`** | 毎フレームの `deltaTime` を録画時と一致させ、フレーム番号で入力を合わせるため |

実時間で再生すると、`deltaTime` のゆらぎでアニメーションの進み方が変わり、
「120 フレーム目に押す」が録画時と違う画面状態に当たる。再現性のためには固定ステップが正しい。

再生中も 01 のスナップショットは取れるので、**再生しながら期待どおりか `expect` で確認**できる。

### 記録の単位

```
DebugOutput/replays/<名前>/
  replay-manifest.json     シード・開始セーブのパス・録画 fps・フレーム数・入力件数・記録時の Unity バージョン
  save-before.json         開始時のセーブ（ゲーム側 Export）
  inputs.jsonl
  recording/               任意。04 の録画を同時に回した場合の動画（人間が見る用）
```

---

## API

```csharp
public sealed class InputRecorder : IDisposable
{
    public void StartRecording(string outputDirectory);
    public ReplayManifest StopRecording();
}

public sealed class InputReplayer : MonoBehaviour   // 使い捨て GameObject 方式
{
    public static InputReplayer StartReplay(string replayDirectory);
    public event Action<ReplayResult> Completed;    // 再生した入力件数・不一致の有無
}
```

シナリオからは `recordInputs: true` / `replay: "<名前>"` で使う。

### ブリッジからの典型的な流れ

1. `seed 12345` → セーブを Export → `InputRecorder.StartRecording`
2. 人間か AI（10）がプレイし、バグを起こす
3. `StopRecording` → `replays/<名前>/` が確定
4. 修正後、`InputReplayer.StartReplay` に `expect` を付けて実行 → 合否

---

## 決定性を壊すものへの対処

| 要因 | 対処 |
|---|---|
| `Time.time` / `deltaTime` 依存 | `captureFramerate` で固定 |
| `System.Random` の未注入な `new Random()` | 静的解析（grep）で洗い出し、`GameRandomSource` へ寄せる。リプレイ実装時の前提作業 |
| `UnityEngine.Random` | `UnityEngine.Random.InitState(seed)` を `seed` コマンドで同時に呼ぶ |
| 実時間依存の演出（`unscaledDeltaTime`、UniTask の Realtime 遅延） | 固定ステップ下でも実時間で進むため不一致になる。**検出して警告**する（再生中の `realtimeSinceStartup` 参照は追えないため、リプレイ結果の `expect` 失敗で気づく） |
| 非同期ロード（Addressables）の完了フレームのゆらぎ | 再生時は「対象が現れるまで待つ」条件で吸収する。入力はフレーム番号ではなく**直前の条件充足からの相対フレーム**で打つオプションを設ける |

最後の項目が実装上の難所である。純粋なフレーム番号再生は、ロード時間が変わると崩れる。
そのため `inputs.jsonl` の各行に**直前ステップの条件（`waitForObject` 等）を記録**しておき、
再生時は条件充足を待ってから相対フレームで打つ「ハイブリッド再生」を既定にする。

---

## 検証方法

- 同じシードで工房→出撃→1戦を 2 回リプレイし、戦闘ログ（`FileLogSink`）が完全一致すること
- 途中で意図的にセーブを変えて再生し、不一致が `ReplayResult` に出ること
- ロードに人工的な遅延を入れても、ハイブリッド再生が同じ結果になること

## スコープ外

- ネットワーク応答の記録・再生（現状オフライン）
- 物理シミュレーションの決定性（3D 導入後に再検討）
