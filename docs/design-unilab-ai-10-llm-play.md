# 10 LLM 駆動の目標プレイ 設計書

ステータス: 設計。ロードマップ M5
依存: 01 UI スナップショット、02 `expect`、04 入力ボキャブラリ。05 があると再現・記録に使える

---

## 目的

「5 階まで行け」「装備を 1 つ製造しろ」のような**目標**を与え、AI が画面を読んで入力を選び、達成するまで回す。
これで得られるものは 3 つ。

1. **プレイ動画**: 人間が操作しなくても、実際のプレイの録画が得られる
2. **探索テスト**: モンキー（06）より賢く、目標に向かう過程で「普通の遊び方」の経路を通る
3. **テストの自己増殖**: 成功した手順を 02 のシナリオとして保存すれば、E2E テストが増えていく

---

## 構成

判断は**エンジンの外**（Claude Code などの LLM エージェント）で行い、エンジン内は観測と操作の口だけを持つ。
UniLab.AI に LLM の API クライアントは入れない。依存を増やさず、モデルの切り替えを自由にするため。

```
┌─ LLM エージェント（Mac 側）────────────────────────┐
│  loop:                                              │
│    obs  = bridge.snapshot()          ← 01 圧縮テキスト│
│    act  = LLM(goal, obs, history)                    │
│    bridge.act(act)                   ← 04 / submit  │
│    obs' = bridge.snapshot()                          │
│    if 差分が空: 「無反応」を history に記す           │
│    if 02.evaluate(goal 条件): 達成 → 終了            │
└──────────────────────┬──────────────────────────────┘
                       │ MCP ブリッジ（execute_code）
┌─ Unity（UniLab.AI）──▼──────────────────────────────┐
│  AgentSession: 目標・手数上限・行動ログ・安全弁       │
│  UiSnapshot / InputInjector / UiScenarioRunner       │
└─────────────────────────────────────────────────────┘
```

### なぜ判断を外に置くか

- ゲームコードに API キーやネットワーク依存を持ち込まない
- モデルの変更・プロンプトの改善をエンジン再起動なしで行える
- Claude Code は既にブリッジで Unity を操作できており、追加の配線が要らない

---

## エンジン内: `AgentSession`

```csharp
/// <summary>LLM エージェントの1セッション分の状態。目標・上限・行動ログ・安全弁を持つ。</summary>
public sealed class AgentSession
{
    public static AgentSession Begin(AgentGoal goal, AgentOptions options);

    /// <summary>現在の観測を AI 向け圧縮テキストで返す。差分があれば差分だけを返すモードも持つ。</summary>
    public string Observe(bool diffOnly);

    /// <summary>1 手を実行し、実行後の観測を返す。禁止操作は拒否してその理由を返す。</summary>
    public string Act(AgentAction action);

    /// <summary>目標条件（02 の expect と同じ語彙）を評価する。</summary>
    public bool IsGoalReached();

    /// <summary>行動ログを 02 のシナリオ JSON として書き出す。成功した手順がそのままテストになる。</summary>
    public string ExportAsScenario(string name);

    public void End();
}
```

### 目標の表現

02 の `expect` と同じ語彙で書く。新しい言語を作らない。

```json
{ "goal": [ { "kind": "gameState", "key": "floor", "op": "ge", "value": "5" } ],
  "maxSteps": 200, "maxSeconds": 600,
  "forbid": ["Delete", "Reset"] }
```

### 行動の表現

04 の語彙に `submit` を足したもの。LLM には**選択肢を列挙して選ばせる**（自由記述にしない）。
選択肢は 01 の `interactable && !blocked` な要素（`click` / `tap` / `submit` の対象）と、
プロジェクトの入力方式に応じた生入力の固定セット（パッドなら A/B/方向、マウスなら右クリック/スクロール等）。
入力方式はセッション開始時の `AgentOptions.inputMode` で指定する。

### 安全弁

- `forbid` に部分一致する要素は `Act` が拒否する（06 の除外リストと同じ仕組み）
- 手数・時間の上限
- 「同じ観測で同じ行動を 3 回」は無限ループとして中断
- 例外が出たら 03 が保存し、`Act` の戻り値に「例外発生」を含めて LLM に知らせる

---

## 観測の設計（トークンを節約する）

- 既定は **01 の圧縮テキスト**。画像は送らない
- `diffOnly=true` で「前回からの差分だけ」を返す。長いプレイで観測が肥大するのを防ぐ
- 画像は**異常時だけ**（差分が空で 3 手続いた、例外が出た、`expect` が失敗した）取り、LLM に「見て判断する」余地を残す
- `game` 状態（ゴールド・階層・HP 等）は毎回含める。目標判定と戦略に直結するため

---

## 記録

セッションごとに `DebugOutput/agent/<session>/`

| ファイル | 内容 |
|---|---|
| `session.json` | 目標・上限・結果（達成 / 上限 / 中断）・手数・所要時間 |
| `actions.jsonl` | 1 手 1 行: 観測の要約・選んだ行動・実行後の差分・LLM の理由（外側が書き込む） |
| `scenario.json` | `ExportAsScenario` の出力。成功時のみ |
| `recording/` | 04 の録画を回していれば動画 |

`scenario.json` は「成功した手順」なので、次回は LLM なしで 02 として再実行できる。
これが**テストの自己増殖**である。失敗したセッションは 05 のリプレイとして残し、バグ再現に使う。

---

## Mac 側の運転手（Claude Code のスキルとして）

`karakuri/.claude/skills/` に `agent-play` スキルを置く。中身は上記ループの手順書。

1. `AgentSession.Begin` をブリッジで呼ぶ
2. `Observe` → 行動を選ぶ → `Act` を、目標達成か上限まで繰り返す
3. 各手の理由を `actions.jsonl` に追記する（外側の責務）
4. 成功したら `ExportAsScenario` し、`docs/debug-scenarios/` へ保存する PR を提案する
5. 失敗したら 03 と 05 の成果物を添えて報告する

---

## 検証方法

- 目標「施設タブを開く」を与え、5 手以内に達成し `scenario.json` が書き出されること
- 目標「鍛冶屋で装備を製造する」を与え、素材不足で達成できない場合に、`game` 状態から理由を報告して止まること（無限に押し続けない）
- `forbid` に `Delete` を入れた状態で「セーブを消す」目標を与え、拒否されること
- 書き出した `scenario.json` を 02 で再実行し `verdict=pass` になること

## スコープ外

- エンジン内での LLM 呼び出し
- 強化学習など、LLM 以外の方策
- リアルタイム性が要る操作（アクションゲーム的な反射）。LLM の応答時間が手番の間に収まる
  ゲーム（手番制・メニュー操作中心）を対象とする。反射が要るジャンルでは 06 のモンキーか 05 のリプレイを使う

## 実装構成（2026-09-05）

`AgentSession` は責務ごとに 4 クラスへ分割し、本体は組み合わせるだけの調停役（約 300 行）にした。公開 API・観測テキスト・`actions.jsonl` / `session.json` / `scenario.json` の出力形式は分割前と同一。

| クラス | 責務 |
|---|---|
| `AgentSession` | `Begin` / `Observe` / `Act` / `End` の入口。予算チェック → forbid → stuck → 実行 → 差分 → 記録 → 目標判定の順序を持つ |
| `AgentActionExecutor` | 1 手 JSON の解釈と `InputInjector` への送出。語彙のパース（ボタン名・方向・キー）と行動キーの生成 |
| `AgentObservationFormatter` | 観測テキストの整形（全文／差分、`actions:` 候補、`game:`、目標未達の理由） |
| `AgentSessionArtifacts` | `actions.jsonl` / `session.json` / 異常時スクショ / `scenario.json` の書き出しと出力ディレクトリ |
| `AgentSessionGuards` | 手数・実時間の予算、同一観測＋同一行動の反復検出、`forbid` の判定 |

外部からの入口は `AiCommandDispatcher`（設計書 12）に統一されており、CLI・メールボックスのどちらも同じ経路で `AgentSessionCommands` → `AgentSession` を呼ぶ。
