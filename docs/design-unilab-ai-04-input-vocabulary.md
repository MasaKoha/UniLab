# 04 入力ボキャブラリ（生入力の注入） 設計書

ステータス: 設計。ロードマップ M3
依存: なし。05・06・10 がこれに依存する

---

## 目的

ランナーの操作は現在 `submit`（名前で指定した要素へ Submit イベントを直接送る）のみ。
これは「ボタンを押す」の代替にはなるが、**プレイヤーの入力とは経路が違う**。

UniLab.AI はプロジェクト横断で使うため、**ゲームパッド・キーボード・マウス・タッチを同格に扱う**。
パッド前提の UI（karakuri）もマウス前提の UI もタッチ前提の UI も、同じ語彙で動かせること。

- 方向キーでフォーカスを動かす、戻るボタン、長押し、テキスト入力ができない
- 選択中タブのボタンは disabled で、`submit` では「タブの内容へ入る（A/Enter 開く）」ができなかった（実際に詰まった）
- 入力→フォーカス解決→操作、という**ゲーム側の入力処理そのもの**を検証できない

プレイヤーと同じ経路で入力を注入し、`submit` は「名前で直接押す」ショートカットとして残す。

---

## 方式

### Unity Input System の仮想デバイス（採用）

利用側（karakuri）は `Gamepad.current` / `Keyboard.current` を直読みしている。
Input System は**仮想デバイスの追加と状態イベントの注入**を公式にサポートしており、
注入した入力はゲームから見て実機と区別できない。

```csharp
var gamepad = InputSystem.AddDevice<Gamepad>("UniLabAI Gamepad");
InputSystem.QueueStateEvent(gamepad, new GamepadState { buttons = 1 << (int)GamepadButton.South });
InputSystem.Update();
```

`Gamepad.current` は最後に入力を受けたデバイスになるため、注入すると仮想デバイスが `current` になる。
実機のパッドが接続されていても、ゲームの読み取り経路は変わらない。

| 方式 | 判定 |
|---|---|
| **Input System 仮想デバイス（採用）** | ゲームと同じ経路。押下・解放・軸・長押しがすべて表現できる |
| `ExecuteEvents` で Submit / Move を送る | フォーカス移動は EventSystem 経由になり、karakuri の自前 `FocusGrid` を通らない。不採用 |
| ゲーム側の入力 interface（`IGamepadInputRelay`）を差し替える | 汎用性が無い（UniLab.AI がゲームの interface を知ることになる）。不採用 |

### 依存の追加

UniLab.AI の asmdef に `Unity.InputSystem` を参照として足す。
Input System を導入していないプロジェクトでもコンパイルが通るよう、`#if ENABLE_INPUT_SYSTEM` で囲み、
無効時は各コマンドが「未対応」を返す。TextMeshPro と同じく Unity 公式パッケージであり、切り出しの妨げにならない。

---

## 入力の語彙

| 語 | 引数 | 動作 |
|---|---|---|
| `press` | ボタン名 | 押して次フレームで離す。`south`(A) / `east`(B) / `north`(Y) / `west`(X) / `dpadUp` 等 / `leftShoulder` / `rightShoulder` / `start` / `select` |
| `hold` | ボタン名, 秒 | 押し続けて指定秒後に離す。長押し操作の検証用 |
| `move` | 方向 | 方向パッドを1回押す（`up` / `down` / `left` / `right`）。フォーカス移動 |
| `stick` | 軸名, x, y, 秒 | アナログスティックを傾け続ける |
| `key` | キー名 | キーボードの1キーを押して離す（`enter` / `escape` / `q` / `e` 等） |
| `text` | 文字列 | `InputSystem.QueueTextEvent` で1文字ずつ送る。`TMP_InputField` へのテキスト入力 |
| `pointerMove` | x, y または 要素名 | 仮想 `Mouse` の位置を動かす。要素名なら中心座標へ |
| `click` | 要素名 または x, y, ボタン(left / right / middle) | 位置へ動かして押して離す |
| `drag` | 始点, 終点, 秒 | 押したまま線形に移動して離す。始点・終点は要素名か座標 |
| `scroll` | x, y, 量 | ホイール。量は正で上 |
| `tap` | 要素名 または x, y | 仮想 `Touchscreen` の 1 指タップ |
| `swipe` | 始点, 終点, 秒 | 1 指のスワイプ |
| `pinch` | 中心, 開始距離, 終了距離, 秒 | 2 指のピンチ |

マウスとタッチは `InputSystem.AddDevice<Mouse>()` / `AddDevice<Touchscreen>()` の仮想デバイスへ
`MouseState` / `TouchState` を投げる。要素名で指定した場合の座標は 01 の `rect` の中心を使い、
遮られていれば（`blockedBy` が空でない）条件待ちで押せるまで待つ。

### シナリオでの書き方

```json
{ "press": "south" },
{ "move": "down" },
{ "move": "down" },
{ "press": "south", "expect": [ { "kind": "sceneIs", "value": "Dungeon" } ] },
{ "hold": "east", "seconds": 1.0 },
{ "text": "ぴーすけ" }
```

`submit` と同じステップに書いてもよいが、1ステップ1操作を推奨する（マーカーと `waited` の対応が崩れるため）。

### 準備待ちとの関係

`submit` は「対象が押せる状態」を待てるが、生入力には対象が無い。
そこで生入力ステップには**明示の待機条件**を書けるようにする。

| 条件 | 意味 |
|---|---|
| `waitForText` | この文字が画面に現れるまで（01 のスナップショットで判定） |
| `waitForObject` | この要素が現れ、遮られていないまで |
| `waitForFocus` | フォーカスがこの要素に来るまで |
| `waitForScene` | 既存の `waitScene` と同じ |

条件が無ければ即送出する。上限は既存と同じ実時間 30 秒。

---

## 「押した結果」の観測

生入力は成功・失敗を返せない（押しただけ）。
何が起きたかは **01 のスナップショット差分**で観測する。ランナーは生入力ステップの前後で
スナップショットを取り、差分が空なら `changed` 相当の警告を出す（`expect` に `changed` を書けば失敗になる）。

---

## API

```csharp
/// <summary>Input System の仮想デバイスを通じて、プレイヤーと同じ経路で入力を注入する。</summary>
public static class InputInjector
{
    public static bool IsSupported { get; }
    public static void Press(GamepadButton button);
    public static IEnumerator Hold(GamepadButton button, float seconds);
    public static void Move(FocusDirection direction);   // UniLab.AI 内で定義する4方向の enum
    public static void Key(Key key);
    public static IEnumerator Text(string text);

    public static void PointerMove(Vector2 screenPosition);
    public static void Click(Vector2 screenPosition, PointerButton button = PointerButton.Left);
    public static IEnumerator Drag(Vector2 from, Vector2 to, float seconds, PointerButton button = PointerButton.Left);
    public static void Scroll(Vector2 screenPosition, float amount);

    public static void Tap(Vector2 screenPosition);
    public static IEnumerator Swipe(Vector2 from, Vector2 to, float seconds);
    public static IEnumerator Pinch(Vector2 center, float fromDistance, float toDistance, float seconds);

    /// <summary>仮想デバイスを取り除く。シナリオ終了時に必ず呼ぶ。</summary>
    public static void Dispose();
}
```

仮想デバイス（Gamepad / Keyboard / Mouse / Touchscreen）は**最初に使われたときに**追加し、
シナリオ終了時（タイムアウト・例外含む）に必ず `RemoveDevice` する。残ると次の Play で `current` が幽霊デバイスになる。
使わないデバイスは追加しない（マウス UI のプロジェクトでパッドを足さない）。

---

## 検証方法

- 工房でホーム到着直後（フォーカスがタブ列）に `press: south` を送り、編成の内容へ入ること
  （`submit: TabButton0` では不可能だった操作）
- `move: down` ×2 で施設タブへフォーカスが移ることをスナップショットの `focused` で確認
- `hold: east` 1 秒で「戻る」長押し系の挙動（あれば）が起きること
- 実機パッドを接続した状態で注入しても、ゲームが注入側を読むこと

## 可視化

注入した入力は 11 のオーバーレイで画面に表示できる（実機入力と同じ経路で拾うため、追加の配線は要らない）。
録画と組み合わせるときは有効にすることを推奨する。

## スコープ外

- ジャイロ・加速度など、センサー系の注入
- 実機タッチスクリーンの物理特性（指の接触面積など）の再現
