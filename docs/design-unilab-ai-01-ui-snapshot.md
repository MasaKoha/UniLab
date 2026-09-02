# 01 UI 状態スナップショット 設計書

ステータス: 設計。ロードマップ M1
依存: なし。02・03・06・10 がこれに依存する

---

## 目的

「いま画面に何があるか」を**画像を見ずに**読めるようにする。
Web エージェントが DOM / accessibility tree を読むのと同じ位置づけ。AI の知覚を画像から構造化データへ移す。

### 既存との違い

| 既存 | 何が足りないか |
|---|---|
| `SceneHierarchyDumper` | 全 GameObject を出す。結線検査には要るが、画面を読むには多すぎる。「押せるか」「何と書いてあるか」「遮られているか」が無い |
| `DebugUiDriver.DumpInteractables`（karakuri） | 押せる要素の名前と文字だけ。静的テキスト・無効な要素・フォーカス・遮蔽・ゲーム状態が無く、JSON でもない |

スナップショットは**画面の意味**を出す。要素の階層ではなく、プレイヤーが見て操作できるものの一覧。

---

## 出力

### JSON（機械処理用）

```json
{
  "capturedAt": "2026-09-02T12:30:00+09:00",
  "frame": 4213,
  "activeScene": "Home",
  "screenWidth": 1397,
  "screenHeight": 786,
  "focusedPath": "Canvas/Content/TabBar/TabButton2",
  "elements": [
    {
      "path": "Canvas/Content/TabBar/TabButton2",
      "name": "TabButton2",
      "kind": "Button",
      "label": "施設",
      "rect": [12, 186, 195, 48],
      "interactable": true,
      "blockedBy": "",
      "focused": true,
      "value": ""
    },
    {
      "path": "Canvas/Content/AssetsBar/GoldText",
      "name": "GoldText",
      "kind": "Text",
      "label": "910G",
      "rect": [12, 20, 60, 30],
      "interactable": false,
      "blockedBy": "",
      "focused": false,
      "value": ""
    }
  ],
  "game": [
    { "key": "gold", "value": "910" },
    { "key": "corePointsTotal", "value": "475" },
    { "key": "activeTab", "value": "Facility" }
  ]
}
```

`JsonUtility` は辞書を扱えないため、`game` はキーと値の配列にする。

### 要素の種別 `kind`

| kind | 判定 | `label` | `value` |
|---|---|---|---|
| Button | `Button` を持つ | 子孫の TMP テキスト（先頭1つ、40 文字で切る） | 空 |
| Toggle | `Toggle` | 同上 | `on` / `off` |
| Slider | `Slider` | 同上 | 現在値 |
| Input | `TMP_InputField` | プレースホルダ | 入力中の文字 |
| Selectable | 上記以外の `Selectable` | 同上 | 空 |
| Text | `TextMeshProUGUI` で、祖先に Selectable が無いもの | 本文（120 文字で切る） | 空 |

`Text` を含めるのが要点。AI が「画面に何と書いてあるか」を読むための行である。
Selectable の子孫にあるテキストは親のラベルとして吸収し、二重に出さない。

### 収集規則

- `FindObjectsByType<Selectable>` と `FindObjectsByType<TextMeshProUGUI>`（非アクティブは除外）
- 祖先に `UiOverlayMarker`（11 の入力可視化オーバーレイ）を持つ要素は除外する。観測器を観測結果に混ぜない
- `Graphic` が無いか `enabled == false` の要素は「見えていない」として除外する
- `rect` は `RectTransformUtility.WorldToScreenPoint` で求めた画面座標（左下原点、x/y/幅/高さ）
- `blockedBy` は条件待ちランナーと同じレイキャスト判定を流用する（対象の中心へ撃ち、最前面が自身か子孫でなければその名前）
- `focused` は `EventSystem.current.currentSelectedGameObject` との一致
- `game` は `GameAdapterRegistry.StateProvider` が登録されていれば同梱。無ければ空配列

### AI 向け圧縮テキスト

同じ内容を、座標と内部パスを省いた行指向テキストで出せるようにする。トークン効率のため。

```
scene=Home focus=TabButton2(施設)
[Button] TabButton0 「編成」
[Button] TabButton1 「ルーン」
[Button] TabButton2 「施設」 *focused
[Button] FacilityCard1/Select 「Lvアップ」
[Text]   GoldText 「910G」
[Text]   CorePointsText 「転生ポイント 475pt」
[Button] ModalCloseButton 「閉じる」 !disabled
[Button] ContinueButton 「Continue」 blocked:PopupDimmer
game: gold=910 corePointsTotal=475 activeTab=Facility
```

`!disabled` と `blocked:<名前>` は AI が「押せない理由」を読むための印。

---

## 差分

```csharp
/// <summary>2つのスナップショットの差分。操作前後で「何が変わったか」を AI が画像なしで読むために使う。</summary>
public sealed class UiSnapshotDiff
{
    public string[] addedPaths;
    public string[] removedPaths;
    public UiSnapshotChange[] changed;   // path / field / before / after
    public string focusedBefore;
    public string focusedAfter;
    public string sceneBefore;
    public string sceneAfter;
    public bool isEmpty;
}
```

`isEmpty == true` が「操作したのに何も変わっていない」の検知になる。
編成タブの `CoreSlotButton`（常時 disabled で押しても無反応だった）は、この差分が空であることで即座に見つかる。

---

## API

```csharp
public static class UiSnapshot
{
    /// <summary>現在の画面状態を収集する。1回の呼び出しで完結し、フレームをまたがない。</summary>
    public static UiSnapshotDocument Capture();

    /// <summary>JSON をファイルへ書き、パスを返す。既定は DebugOutput/snapshots/。</summary>
    public static string Save(UiSnapshotDocument document, string outputDirectory = null);

    /// <summary>AI 向けの圧縮テキストへ変換する。</summary>
    public static string ToCompactText(UiSnapshotDocument document);

    /// <summary>差分を取る。</summary>
    public static UiSnapshotDiff Compare(UiSnapshotDocument before, UiSnapshotDocument after);
}
```

### 呼び出し口

- `execute_code`: `return UniLab.AI.UiSnapshot.ToCompactText(UniLab.AI.UiSnapshot.Capture());`
- シナリオ（02）: ステップに `snapshot: "<名前>"` を書くと JSON を保存する
- 例外フォレンジック（03）が例外時に自動で呼ぶ

---

## 性能

診断用途で1回ごとの呼び出しであり毎フレーム経路ではないため、`FindObjectsByType` / `GetComponentInChildren` を許容する。
ただし 06 モンキーテスターは高頻度に呼ぶため、要素数が数百を超えるシーンでは 1 回あたりの所要時間をログに出し、
必要なら `Text` を除外するオプションを設ける。

---

## 検証方法

- 工房の各タブでスナップショットを取り、目視した画面と要素一覧が一致すること（今日の 14 枚を照合）
- 施設タブでガイドバーの `Text` に「取り外し」が含まれないことを、画像を開かずに確認できること
- モーダル表示中、背面要素の `blockedBy` に `PopupDimmer` が入ること
- 編成の `CoreSlotButton` が `interactable=false` で出ること

## スコープ外

- 3D オブジェクトの列挙（UI に限る。3D は 07 の画像比較で扱う）
- 要素の見た目（色・フォント）の記録
