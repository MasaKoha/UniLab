# UniLab.AI ロードマップ — AI がデバッグとプレイを自律的に回すためのツール群

作成日: 2026-09-02
ステータス: 設計（全体方針）。個別設計は `design-unilab-ai-01` 〜 `-11` を参照
前提: `design-unilab-ai-tools.md`（UniLab.AI の境界・録画・条件待ちランナー、実装済み）

---

## 目的

AI エージェントが **知覚 → 判断 → 操作 → 確認** のループを、人間の目を借りずに閉じられるようにする。

現状の UniLab.AI は「見る（スクショ・動画・監査・階層）」と「動かす（条件待ちシナリオ）」の基礎がある。
しかし確認の最終段は**人間か AI が画像を開いて目で見る**ことに依存している。
2026-09-02 の検証では 14 枚の画像を目視した。画像は AI にとって高価で曖昧であり、ここが自律化のボトルネックである。

本ロードマップは、この目視を**構造化データの比較**に置き換え、そのうえに自動探索・再現・目標プレイを積む。

---

## 全体像と依存関係

```
                 ┌──────────────────────────────┐
                 │ 01 UI 状態スナップショット      │ ← すべての「目」
                 └──────┬─────────┬─────────┬───┘
                        │         │         │
        ┌───────────────▼──┐  ┌───▼─────┐  ┌▼──────────────────┐
        │ 02 expect + 合否 │  │ 06 モンキー│  │ 10 LLM 目標プレイ │
        └────────┬─────────┘  └───┬─────┘  └────────┬──────────┘
                 │                │                  │
   ┌─────────────▼──┐     ┌───────▼────────┐  ┌──────▼─────────┐
   │ 03 例外フォレンジック│     │ 04 入力ボキャブラリ │  │ 05 決定的リプレイ │
   └────────────────┘     └────────────────┘  └────────────────┘

   横断: 07 視覚回帰 / 08 性能計測 / 09 RunArchive + ギャラリー / 11 入力可視化オーバーレイ
```

| # | ツール | 依存 | 規模 |
|---|---|---|---|
| 01 | UI 状態スナップショット | なし | 中 |
| 02 | シナリオ `expect` と合否 JSON | 01 | 中 |
| 03 | 例外時フォレンジック | 01 | 小 |
| 04 | 入力ボキャブラリ（生入力注入） | なし | 中 |
| 05 | 決定的リプレイ（シード＋入力記録） | 04 | 中〜大 |
| 06 | モンキーテスター | 01, 03, 04 | 小〜中 |
| 07 | 視覚回帰 | なし | 中 |
| 08 | 性能計測 | なし | 小 |
| 09 | RunArchive とスマホ閲覧 | なし（成果物の集約） | 中 |
| 10 | LLM 駆動の目標プレイ | 01, 02, 04 | 大 |
| 11 | 入力可視化オーバーレイ | なし（録画機能の一部。01・07 が除外を実装） | 小〜中 |
| 12 | AI ゲートウェイ（単一ディスパッチャ＋Unity 内蔵メールボックス） | 01, 10 | 中 |

### 実装順

1. **M1: 11 + 01 + 02** — 録画に「何を押したか」を写し（動画が主用途なので最初に入れる）、
   「目を使わずに読める状態」と「合否が返る」を揃える。今日の目視作業の大半がこれで自動化される
2. **M2: 03 + 06** — 勝手に叩いて、壊れたら証拠を残す。安いクラッシュ発見
3. **M3: 04 + 05** — プレイヤーと同じ入力で動かし、バグを決定的に再現する
4. **M4: 07 + 08 + 09** — 回帰検出と成果物の整理
5. **M5: 10** — 目標を与えて遊ばせる。成功した手順はシナリオとして保存し、テストを自己増殖させる

---

## 共通の設計方針

### 1. UniLab.AI は汎用のまま。ゲーム固有は interface で差し込む

**UniLab.AI は karakuri 専用ではない。** 他のプロジェクトでもそのまま使う前提で設計する。
各設計書で karakuri の画面や要素名が出てくるのは**動作を確かめる実例**としてであり、要件ではない。
特に入力は、ゲームパッド・キーボード・マウス・タッチを同格に扱う（04・11）。

UniLab.AI は `UniLab` 本体・R3・UniTask・VContainer に依存しない（既定方針）。
「ゴールドを読む」「階層へ飛ぶ」「シードを固定する」はゲーム固有なので、UniLab.AI には**口だけ**置く。

```csharp
/// <summary>ゲーム側が実装する、構造化された状態の読み出し口。スナップショットに `game` として同梱される。</summary>
public interface IGameStateProvider
{
    /// <summary>キーと値の平坦な辞書。値は数値・文字列・真偽のみ（JSON 化のため）。</summary>
    IReadOnlyDictionary<string, object> GetState();
}

/// <summary>ゲーム側が実装する、名前付きコマンドの実行口。チート・シード固定・状態遷移など。</summary>
public interface IGameCommandHandler
{
    /// <summary>実行できるコマンド名の一覧。AI が発見できるように公開する。</summary>
    IReadOnlyList<string> CommandNames { get; }

    /// <summary>コマンドを実行し、結果メッセージを返す。未知のコマンドは false。</summary>
    bool TryExecute(string commandName, IReadOnlyDictionary<string, string> arguments, out string message);
}

/// <summary>UniLab.AI がゲーム側の実装を受け取る登録口。DI に依存しないため静的に持つ。</summary>
public static class GameAdapterRegistry
{
    public static IGameStateProvider StateProvider { get; set; }
    public static IGameCommandHandler CommandHandler { get; set; }
}
```

karakuri は `BootLifetimeScope` で実装を登録する。UniLab.AI 側は登録が無ければその機能を黙って省く（例外にしない）。

### 2. 入口は MCP ブリッジの `execute_code` からの静的メソッド

メニュー項目は引数を取れないため、AI からの主経路は**静的メソッド呼び出し**とする。
将来は1つの JSON ディスパッチャ `AiToolCommands.Execute(string json)` に集約し、
コマンド名と引数で全ツールへ届くようにする（各設計書の「呼び出し口」節）。

### 3. 出力は JSON（`JsonUtility`）＋ AI 向け圧縮テキスト

- 機械処理用: `[Serializable]` public フィールドの JSON。`JsonUtility` の制約（辞書不可・ネスト制限）に合わせて平坦に設計する
- AI 読解用: 同じ内容の**行指向テキスト**を併記できるようにする。トークン効率のため座標や内部 ID を省く

### 4. 出力先は `DebugOutput/` 配下に種別ごと

```
DebugOutput/
  recordings/<name>/          動画（既存）
  snapshots/<timestamp>.json  01
  scenario-results/<name>.json 02
  forensics/<timestamp>-<n>/  03
  replays/<name>/             05
  monkey/<run>/               06
  visual-regression/<run>/    07
  performance/<run>.json      08
  agent/<session>/            10
VerificationRuns/run-<ts>/    09 が上記をラン単位に集約
```

### 5. 観測器は観測対象を変えない

条件待ちランナーで確立した原則。フレーム数の待ち・余白・固定タイムステップを持ち込まない。
例外は 05 のリプレイで、**再現性のために意図して** `Time.captureFramerate` を使う（設計書に理由を明記する）。

### 6. 「成功した」を自己申告させない

各ツールの結果は「何をしたか」ではなく**「何が観測されたか」**を返す。
撮影枚数や尺で成功を判定した過去の失敗（ダイアログが写り込んだ動画を「撮れた」と報告）を繰り返さない。

---

## 何を UniLab.AI に置き、何を利用側に置くか

| 置き場 | 内容 |
|---|---|
| `Assets/UniLab.AI/Runtime/` | 01〜08, 10, 11 の本体。ゲーム非依存 |
| `Assets/UniLab.AI/Editor/` | メニュー・エディタ専用処理（07 の画像比較、09 の集約） |
| 利用側（karakuri）`Assets/_Project/` | `IGameStateProvider` / `IGameCommandHandler` の実装、ブリッジ用の薄いメニュー、シナリオ JSON、ベースライン画像 |
| `karakuri/tools/` | 09 の `serve_gallery.py`（Python・プロジェクト横断） |

切り出し手順は従来どおり「`Assets/UniLab.AI/` を丸ごと移す」で変わらない。

---

## 各設計書

| # | ファイル |
|---|---|
| 01 | `design-unilab-ai-01-ui-snapshot.md` |
| 02 | `design-unilab-ai-02-scenario-expect.md` |
| 03 | `design-unilab-ai-03-exception-forensics.md` |
| 04 | `design-unilab-ai-04-input-vocabulary.md` |
| 05 | `design-unilab-ai-05-deterministic-replay.md` |
| 06 | `design-unilab-ai-06-monkey-tester.md` |
| 07 | `design-unilab-ai-07-visual-regression.md` |
| 08 | `design-unilab-ai-08-performance-recorder.md` |
| 09 | `design-unilab-ai-09-run-archive.md` |
| 10 | `design-unilab-ai-10-llm-play.md` |
| 11 | `design-unilab-ai-11-input-overlay.md` |
| 12 | `design-unilab-ai-12-ai-gateway.md` |
