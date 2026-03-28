# リファクタリング方針 確認事項

---

## P0 — バグ・確実に壊れている箇所

**Q1.** `SingletonMonoBehavoire` はタイポなので `SingletonMonoBehaviour` にリネームしますか？
参照箇所が多いため、全ファイルの using・継承元も一括変更します。
OK

**Q2.** `ToastManager` で `async UniTaskVoid` メソッドをさらに `.Forget()` している二重呼び出しがあります。`UniTaskVoid` に統一しますか？それとも `async UniTask` + `.Forget()` に統一しますか？
UniTaskVoid で

**Q3.** `SceneFadeTransition` で `OperationCanceledException` を catch して rethrow していますが、DOTween の tween が `Kill()` されない可能性があります。`finally` ブロックで `tween.Kill()` する形に変更しますか？
変更

**Q4.** `InputBlock` と `LoadingInputBlock` が全く同じ構造で2クラス重複定義されています。共通の基底クラスに統合しますか？それとも1クラスに統一しますか？
共通基底クラス

---

## P1 — 設計の問題

**Q5.** `SwipeDirection` enum が `BannerCellBase` と `SwipeDetector` の2か所に重複定義されています。`SwipeDetector` 側に一本化して `BannerCellBase` 側を削除しますか？
１本化

**Q6.** `AnimationPlayer.PlayAsync()` に3つのオーバーロードがあり、ロジックが重複しています。デフォルト引数を使って1メソッドに統一しますか？
1メソッド統一

**Q7.** `ScreenManagerBase` と `UniLabSceneManagerBase` で `Pop` が複数箇所から呼ばれていて追いにくくなっています。1メソッドに集約してコメントで意図を明示する形にリファクタリングしますか？
リファクタリング

**Q8.** `LocalizationData` の `BuildHashMap` が呼ばれるたびに条件次第で再構築される実装になっています。`Lazy<T>` で一度だけ初期化する形に変更しますか？
OK

**Q9.** `MasterManager` の `try-catch` が全例外を飲み込んでいます。特定の例外型に絞りますか？その場合、どの例外を対象にするか確認が必要です。
そのように

---

## P2 — コーディング規約・コメント

**Q10.** `AuthUser` が `public string UserId;` のような public フィールド直接公開になっています。`public string UserId { get; set; }` プロパティに変更しますか？（`[Serializable]` は維持）
継続で大丈夫

**Q11.** `VariableGridLayoutGroup` に4段ネストがあります。メソッド抽出で平坦化しますか？
もし可能なら平坦化

**Q12.** `MasterManager` に `if → if → if` の深いネストがあります。早期 return で平坦化しますか？
OK

**Q13.** `ApiClientBase` の `CloneRequest` 内でヘッダ再設定のロジックが重複しています。`SetCommonHeaders` ヘルパーメソッドに一本化しますか？
OK

**Q14.** `UniLabButton`・`InputBlock`・`SoundPlayManager` など、public メンバーへの `<summary>` コメントが漏れている箇所があります。一括で追加しますか？
追加

---

## スコープ確認

**Q15.** 以下は今回のスコープ外としましたが、含めますか？
- `BannerViewBase` の `List` → `LinkedList` 化（ローテーション処理のパフォーマンス改善） 含める
- `SwipeDetector` のプラットフォーム分岐を Strategy パターン化 含める
- `LocalizationData` の thread-safe 化 含める

---

## テスト

**Q16.** 修正したクラスのテストは修正と同時に書きますか？それとも全修正が終わった後にまとめて書きますか？
同時に書く

**Q17.** 現在テストがない `AnimationPlayer`・`ScreenManagerBase`・`PopupManagerBase` の基本フローについて、PlayMode テストを追加しますか？
テスト追加

**Q18.** `LocalizationData` / `TextManager` の Parse ロジックについて、EditMode テストを追加しますか？（既存の `ParseCsvLineTest` があるため、重複しないよう範囲を決める必要があります）追加
