# 02 シナリオ `expect` と合否 JSON 設計書

ステータス: 設計。ロードマップ M1
依存: 01 UI スナップショット

---

## 目的

シナリオを「撮影する台本」から「**合否を返すテスト**」にする。
現状のランナーは撮影・録画・監査を行うが、pass/fail を出さない。結果の判断は人間か AI が画像を見て行っている。

`expect` を書けば、実行結果は JSON の合否と証拠パスで返る。AI はそれを読んで次の行動を決められる。

---

## ステップの拡張

```json
{ "submit": "TabButton2",
  "expect": [
    { "kind": "textVisible", "value": "施設" },
    { "kind": "textAbsent",  "value": "取り外し", "scope": "InputGuideBar" },
    { "kind": "exists",      "target": "FacilityCard1/Select" },
    { "kind": "interactable","target": "FacilityCard1/Select" },
    { "kind": "noException" },
    { "kind": "auditClean" },
    { "kind": "gameState",   "key": "activeTab", "op": "eq", "value": "Facility" }
  ]
}
```

### `kind` 一覧

| kind | 判定 | 入力 |
|---|---|---|
| `textVisible` | スナップショットの `label` に部分一致する `Text` か `Button` がある | `value`、任意で `scope`（パスの接頭） |
| `textAbsent` | 上の否定 | 同上 |
| `exists` / `absent` | `target` パスの要素がある / ない | `target` |
| `interactable` / `disabled` | `target` が操作可能 / 不能 | `target` |
| `focused` | フォーカスが `target` にある | `target` |
| `sceneIs` | アクティブシーン名 | `value` |
| `noException` | このステップの開始から評価時点までに Exception / Error ログが出ていない | なし |
| `auditClean` | `UiLayoutAuditor` の検出が 0 件 | なし |
| `gameState` | `IGameStateProvider` の値を比較 | `key`, `op`（eq / ne / lt / le / gt / ge / contains）, `value` |
| `changed` | 直前ステップのスナップショットと差分がある（「押したのに変わらない」の検知） | なし |
| `noDroppedFrames` | 録画中なら捨てたフレームが 0 | なし |

評価は**操作後・`settleFrames` 経過後**に行う（撮影・監査と同じタイミング）。
`expect` を持つステップは、`settleFrames` 未指定でも既定 30 を待つ（撮影と同じ扱い）。

### 失敗時の挙動

- 既定: 記録して続行する（他のステップの結果も集めるため）
- `"stopOnFail": true` をシナリオ直下に書くと、最初の失敗で中断する
- 失敗したステップでは、証拠として**スナップショット JSON とスクリーンショットを自動保存**する（`expect` の有無に関わらず撮る）

---

## 結果 JSON

`DebugOutput/scenario-results/<シナリオ名>-<timestamp>.json`

```json
{
  "scenario": "full-screen-tour",
  "verdict": "fail",
  "startedAt": "...", "finishedAt": "...", "durationSeconds": 31.2,
  "stepCount": 28, "passedSteps": 27, "failedSteps": 1,
  "exceptionCount": 0, "warningCount": 0, "droppedFrameCount": 0,
  "steps": [
    {
      "index": 8, "submit": "FacilityCard1/Select", "status": "pass",
      "waitedSeconds": 0.03, "failures": [],
      "evidence": { "capture": "", "snapshot": "" }
    },
    {
      "index": 9, "submit": "ModalCloseButton", "status": "fail",
      "waitedSeconds": 30.0,
      "failures": [
        { "kind": "exists", "target": "ModalCloseButton", "message": "要素が見つかりません" },
        { "kind": "textAbsent", "value": "取り外し", "message": "InputGuideBar に「X 取り外し」が表示されています" }
      ],
      "evidence": {
        "capture": "DebugOutput/scenario-results/full-screen-tour-.../step09.png",
        "snapshot": "DebugOutput/scenario-results/full-screen-tour-.../step09.json"
      }
    }
  ],
  "recordings": ["DebugOutput/recordings/full_tour/"],
  "exceptions": []
}
```

`verdict` は `pass` / `fail` / `error`（ランナー自身の異常）。
**ランナーが「見送った」送出（対象が現れない・遮られたまま・操作不能）は自動的に失敗扱い**とする。
旧ランナーが黙って捨てていた事故を、合否として必ず表面化させるため。

---

## API と呼び出し口

```csharp
public sealed class UiScenarioRunner
{
    /// <summary>完了時に結果 JSON のパスを渡す。ブリッジからの利用者はこのファイルをポーリングする。</summary>
    public event Action<string> ResultSaved;
}

public static class UiScenarioRunnerMenu
{
    /// <summary>シナリオを実行し、結果 JSON の出力先パスを即座に返す（完了は待たない）。</summary>
    public static string RunScenarioFile(string scenarioPath);
}
```

ブリッジ（`execute_code`）は1回の呼び出しでフレームをまたげないため、**結果はファイルで受け取る**。
呼び出し側は返されたパスにファイルが現れるまでポーリングする。これは現在の録画 manifest と同じ運用である。

---

## 既存シナリオへの適用例

今日目視で確認した4件は、すべて `expect` で書ける。

```json
{ "submit": "TabButton2", "expect": [
    { "kind": "textAbsent", "value": "取り外し", "scope": "InputGuideBar" } ] },
{ "submit": "TabButton0", "expect": [
    { "kind": "textVisible", "value": "武器:" },
    { "kind": "textAbsent",  "value": "Weapon:" },
    { "kind": "textVisible", "value": "必要: 攻撃+呪い" } ] }
```

ルーンスロットの自動縮小（見た目の問題）だけは `expect` で表現できない。これは 07 視覚回帰の領域。

---

## 検証方法

- 全画面巡回シナリオに `noException` と `auditClean` を全ステップへ付け、`verdict=pass` が返ること
- 廃止済みの `CoreSlotButton` ステップを一時的に戻し、`verdict=fail` と「操作不能」の失敗が記録されること
- `stopOnFail` で最初の失敗後にステップが実行されないこと

## スコープ外

- 画像の一致判定（07）
- 性能しきい値（08。ただし 08 の結果を `expect` の `kind` として後から足せる設計にしておく）
