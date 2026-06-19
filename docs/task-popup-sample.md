# Popup 基盤 v2 動作確認サンプル

作成日: 2026-06-19 / 更新日: 2026-06-20 / 対象ブランチ: `feature/popup-foundation`

## 目的

Popup 基盤 v2（`IPopupService` / `PopupService` / `IPopupViewProvider` / `PopupBase<TParameter,TResult>`）を Unity 実機で動作確認する。

## 構成

- **C# スクリプト**（`Assets/UniLab/Sample/Popup/`, namespace `UniLab.UI.Popup.Sample`）
  - `RewardPopupResult.cs` … 任意結果型のデモ（readonly struct）
  - `RewardPopupParameter.cs` … `IPopupParameter` 実装
  - `RewardPopup.cs` … `PopupBase<RewardPopupParameter, RewardPopupResult>`。フェード（UiTween）
  - `PopupSampleEntry.cs` … 起点。3ボタンで v2 を実演。プレハブは Resources からロードする内蔵 `ResourcesPopupViewProvider` を使う（Editor のアセット参照配線に依存しない）
- **シーン/プレハブ生成**（`Assets/UniLab/Sample/Popup/Editor/`）
  - `PopupSampleBuilder.cs` … メニュー `UniLab > Sample > Build Popup Sample` でシーン・プレハブを一括生成
  - プレハブ出力先: `Assets/UniLab/Sample/Popup/Resources/Popup/`（`ConfirmPopup.prefab` / `RewardPopup.prefab`）

## 設計メモ：Resources ロード方式

Editor スクリプトの `SerializedObject` 経由では「シーン → プレハブアセット」への参照配線がこの環境で安定しなかったため、
プレハブは `Resources.Load<TPopup>($"Popup/{型名}")` でロードする。これにより Editor でのプレハブ参照配線が不要になる。
シーン内参照（ボタン / `_popupRoot` / `_buttonGroup`）は `SerializedObject` で確実に配線できるためそのまま使う。

## 使い方（Unity 上）

1. コンパイルを通す
2. メニュー `UniLab > Sample > Build Popup Sample` を実行（既存のシーン・プレハブを上書き再生成）
3. `Assets/UniLab/Sample/Popup/PopupSample.unity` を Play して3ボタンを押す

## 検証シナリオ

1. **v2 確認ダイアログ**（Confirm ボタン）: `ShowAsync<ConfirmPopup, PopupResult>` → Yes/No/背景タップ
2. **任意結果型**（Reward ボタン）: `ShowAsync<RewardPopup, RewardPopupResult>` → `claimed` / `amount` が返る
3. **直列化＋優先度**（Priority Test ボタン）: System→High→Normal→Low の順で1つずつ表示
4. **入力ブロック**: 表示中は `HasActivePopup` 購読で `_buttonGroup.interactable=false`
5. **リーク無し**: 連打・表示中の別要求でも GameObject が残らない（finally で必ず Release）

## 規約

ブロック namespace / `if`・`for` は波括弧 / 省略形禁止 / 不要 null チェック禁止 / public に `/// <summary>` /
コメントは日本語 / DOTween 禁止（UiTween 使用）/ `IDisposable` の Dispose 漏れ禁止。
