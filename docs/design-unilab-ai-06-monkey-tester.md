# 06 モンキーテスター 設計書

ステータス: 設計。ロードマップ M2
依存: 01 UI スナップショット、03 例外フォレンジック、04 入力ボキャブラリ（無くても `submit` で動く）

---

## 目的

人間が書いたシナリオは「想定した道」しか通らない。
**想定していない順序・組み合わせ**でクラッシュや詰まりを掘るには、ランダムに叩く係が要る。
安価で、放置で回せて、壊れたら証拠を残す。

---

## 動作

```
loop (時間か手数の上限まで):
  snapshot = 01.Capture()
  candidates = snapshot.elements で interactable && blockedBy 空 && 除外リストに無いもの
  if candidates が空:
      「詰まり」として記録 → 脱出手（戻る入力）を試す → それでも空なら停止
  action = 重み付きで選ぶ（未訪問のパスを優先、直前と同じものは避ける）
  before = snapshot
  操作する（submit、または 04 があれば press/move）
  条件待ち（対象が押せる状態 → 操作 → settle 既定 0）
  after = 01.Capture()
  不変条件を検査する
  trace に1行書く
```

### 不変条件

| 条件 | 違反時 |
|---|---|
| 例外・エラーログが出ていない | 03 が自動保存。trace に紐づけ、`stopOnViolation` なら停止 |
| 操作後 N 秒以内にスナップショットに変化がある | 「無反応」として記録。同じ要素で 3 回続いたら除外リストへ入れる（常時 disabled の穴を自動で避ける） |
| 押せる要素が 1 つ以上ある | 0 なら「詰まり」。戻る入力で脱出を試み、失敗なら停止（脱出不能はそれ自体がバグ） |
| K 手ごとの `UiLayoutAuditor` が 0 件 | 検出をフォレンジック相当で保存 |
| フレーム時間が上限以内（08 があれば） | 記録のみ |

### 除外リスト

破壊的な操作を避けるため、名前パスの部分一致で除外できる。既定で `Delete`・`Reset`・`Quit` を含む。
karakuri では `Delete Save` に相当する要素を必ず入れる。除外はシナリオ側の設定で上書きできる。

### 探索の重み

- 未訪問パスを優先（カバレッジを広げる）
- 同じ画面に長く居たら「タブ切り替え」「戻る」系を優先（局所に閉じ込められない）
- 完全ランダムではなく**シード付き**にし、同じシードなら同じ手順になる（05 と組み合わせて再現できる）

---

## 出力

`DebugOutput/monkey/<run-timestamp>/`

| ファイル | 内容 |
|---|---|
| `trace.jsonl` | 1手1行: 手番・フレーム・操作した要素・操作前後のシーン・差分の有無・違反の有無・待った秒数 |
| `coverage.json` | 訪れた画面（シーン＋アクティブタブ等の `game` 状態）と押した要素の一覧・回数 |
| `violations.json` | 違反の一覧と 03 のフォルダへの参照 |
| `summary.json` | 手数・所要時間・違反数・カバレッジ・停止理由 |

録画（04 の `recordStart`）を同時に回せる。違反の瞬間は `trace` のフレーム番号から動画の秒へ辿れる。

---

## API

```csharp
public sealed class MonkeyTester : MonoBehaviour   // 使い捨て GameObject 方式
{
    public static MonkeyTester Start(MonkeyOptions options);
    public event Action<MonkeySummary> Completed;
}

public sealed class MonkeyOptions
{
    public int seed;
    public int maxSteps = 500;
    public float maxSeconds = 300f;
    public string[] excludePathContains = { "Delete", "Reset", "Quit" };
    public bool stopOnViolation = false;
    public bool useRawInput = true;        // 04 があれば press/move を混ぜる
    public float noChangeTimeoutSeconds = 2f;
}
```

シナリオからは `{ "monkey": { "seed": 1, "maxSteps": 300 } }` を1ステップとして書ける。
その前後に通常のステップ（特定画面まで進める、録画を止める）を置ける。

---

## 何が見つかると期待するか

- 常時 disabled なのに残っている要素（編成の `CoreSlotButton` のような腐敗）→「無反応」で浮く
- 特定の順序でだけ出る例外（モーダルを開いたまま別タブ、など）
- 脱出不能な画面（戻る入力で戻れない）
- ランダムに叩いた結果の見た目の破綻（`auditClean`）

---

## 検証方法

- 工房で 300 手・シード固定で 2 回回し、`trace.jsonl` が一致すること（再現性）
- 除外リストに `Delete` を入れた状態でセーブが消えないこと
- 意図的に例外を投げるボタンを一時的に置き、違反として捕まり 03 のフォルダが作られること

## スコープ外

- 目標を持った探索（10）
- 入力の意味理解（「このボタンは購入だから金が要る」等は判断しない）
