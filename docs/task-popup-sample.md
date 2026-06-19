# タスク: Popup 基盤 v2 動作確認サンプルの実装

作成日: 2026-06-19 / 対象ブランチ: `feature/popup-foundation`

## 目的

実装済みの Popup 基盤 v2（`IPopupService` / `PopupService` / `IPopupViewProvider` / `PopupBase<TParameter,TResult>`）を、
Unity 実機で動作確認するためのサンプル。あわせて v1 後方互換（`UniLabPopupManager`）も確認する。

## 構成

- **C# スクリプト**（`Assets/UniLab/Sample/Popup/`, namespace `UniLab.UI.Popup.Sample`）
  - `RewardPopupResult.cs` … 任意結果型のデモ（readonly struct）
  - `RewardPopupParameter.cs` … `IPopupParameter` 実装
  - `RewardPopup.cs` … `PopupBase<RewardPopupParameter, RewardPopupResult>`。フェード（UiTween）
  - `PopupSampleEntry.cs` … 起点。各ボタンで v2/v1/優先度/入力ブロックを実演
  - `UniLab.UIComponent.Sample.asmdef`
- **シーン/プレハブ生成**（`Assets/UniLab/Sample/Popup/Editor/`）
  - `PopupSampleBuilder.cs` … メニュー `UniLab > Sample > Build Popup Sample` でシーン・プレハブ・配線を一括生成
  - `UniLab.UIComponent.Sample.Editor.asmdef`

## 公開 API（呼び出し側）

```csharp
UniTask<TResult> ShowAsync<TPopup, TResult>(IPopupParameter parameter, CancellationToken ct = default)
    where TPopup : PopupBase<TResult>;
ReadOnlyReactiveProperty<bool> HasActivePopup { get; }
UniTask CloseTopAsync();
public PopupService(IPopupViewProvider viewProvider);   // IDisposable

new PopupParameter { Title, Message, ConfirmLabel="OK", CancelLabel=null };
enum PopupResult { None, Confirm, Cancel }
enum PopupPriority { None, Low, Normal, High, System }
```

## 使い方（Unity 上）

1. Unity でコンパイルを通す
2. メニュー `UniLab > Sample > Build Popup Sample` を実行
   - `Assets/UniLab/Sample/Popup/PopupSample.unity` と `Prefabs/ConfirmPopup.prefab` / `RewardPopup.prefab` が生成・配線される
3. Play して4ボタンを押す

> batchmode 生成も可:
> `Unity -batchmode -projectPath <repo> -executeMethod UniLab.UI.Popup.SampleEditor.PopupSampleBuilder.Build -quit -logFile -`
> （同じプロジェクトを Unity Editor で開いていないこと）

## 検証シナリオ

1. v2 確認ダイアログ（Confirm/Cancel/背景タップ）
2. 任意結果型（RewardPopupResult の claimed/amount）
3. 直列化＋優先度（System→High→Normal→Low の順で処理）
4. 入力ブロック（表示中は `_buttonGroup.interactable=false`）
5. v1 後方互換（`UniLabPopupManager.ShowAsync`）
6. リーク無し（Hierarchy に GameObject が残らない）

## 規約

ブロック namespace / `if`・`for` は波括弧 / 省略形禁止 / 不要 null チェック禁止 / public に `/// <summary>` /
コメントは日本語 / DOTween 禁止（UiTween 使用）/ `IDisposable` の Dispose 漏れ禁止。
