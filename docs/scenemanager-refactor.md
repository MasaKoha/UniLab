# SceneManager 系 改修設計
作成日: 2026-03-28

---

## 改修方針

現状の SceneManager 系は Presenter ロジックが MonoBehaviour に直書きされており、
VContainer による DI・純粋 C# Presenter パターンと非互換。
以下の方針で改修する。

- MonoBehaviour はラッパーとして残す（Unity イベント・Inspector アサインのみ担当）
- ロジックは純粋 C# クラスに切り出す
- VContainer の LifetimeScope と連携できる構造にする

---

## 改修しないモジュール

以下は MonoBehaviour のまま変更しない。

- PopupManagerBase / PopupBase
- SoundPlayManager
- LocalSave
- MobileLocalNotification
- UISafeArea / UniLabButton
- AesEncryptionUtility / StringUtility
- MasterManager / MasterCatalog / MasterBase

---

## 新クラス構造

### ScreenBase

改修前:
- ScreenBase : MonoBehaviour
- Presenter ロジック直書き

改修後:
- IScreenView（インターフェース）← Presenter が参照する契約
- ScreenBase : MonoBehaviour, IScreenView ← MonoBehaviour ラッパーのみ
- ScreenPresenterBase（純粋 C# 基底クラス）← IInitializable, IDisposable を実装

IScreenView の責務:
- Show() ← 画面がアクティブになるときに呼ばれる
- Hide() ← 画面が非アクティブになるときに呼ばれる

ScreenBase の責務:
- Show() → gameObject.SetActive(true)
- Hide() → gameObject.SetActive(false)
- Inspector アサインのみ（[SerializeField] はここにのみ書く）
- GetComponent 禁止（事前アサイン必須）

ScreenPresenterBase の責務:
- Initialize() を abstract で定義（VContainer の IInitializable 経由で呼ばれる）
- Dispose() で CompositeDisposable を破棄
- Subscribe はすべて Initialize() 内で行う

---

### ScreenManagerBase

改修前:
- ScreenManagerBase : MonoBehaviour
- 画面切り替えロジック直書き

改修後:
- IScreenManager（インターフェース）← Presenter から参照する契約
- ScreenManagerBase : MonoBehaviour, IScreenManager ← MonoBehaviour ラッパー

IScreenManager の責務:
- ShowScreen<T>() ← 指定した型の画面に遷移する
- Back() ← 前の画面に戻る
- OnScreenChanged : IObservable<IScreenView> ← 画面切り替えを通知する

ScreenManagerBase の実装方針:
- 画面履歴を Stack<IScreenView> で管理する
- ShowScreen<T>() では現在画面を Hide() し、新画面を Push して Show() する
- Back() では現在画面を Pop し、前画面を Show() する
- 画面の登録は RegisterScreen<T>() を LifetimeScope から呼ぶ
- Subject<IScreenView> を IObservable<IScreenView> にラップして OnScreenChanged として公開する

---

### SceneMainBase

改修前:
- SceneMainBase : MonoBehaviour
- シーン初期化ロジック直書き

改修後:
- SceneMainBase : MonoBehaviour
- OnDestroy で LifetimeScope の Dispose を呼ぶだけ
- 初期化ロジックはすべて VContainer の IInitializable に委譲する

SceneMainBase の責務:
- [SerializeField] で LifetimeScope を参照する
- OnDestroy() で _lifetimeScope.Dispose() を呼ぶ
- それ以外のロジックは一切持たない

---

### UniLabSceneManagerBase

改修前:
- UniLabSceneManagerBase : MonoBehaviour
- シーン遷移ロジック直書き、DI なし

改修後:
- ISceneManager（インターフェース）← Presenter から参照する契約
- UniLabSceneManagerBase : MonoBehaviour, ISceneManager ← AppLifetimeScope に Singleton 登録

ISceneManager の責務:
- LoadSceneAsync(sceneName, token) ← 指定シーンを Additive ロードし現在シーンをアンロード
- SetParameter<T>(parameter) ← 次シーンへの一時パラメータをセット
- GetParameter<T>() ← このシーン向けにセットされたパラメータを取得

SceneParameterBase:
- シーン遷移時の一時パラメータ基底クラス
- 各シーン固有のパラメータはこのクラスを継承して定義する
- 例: InGameSceneParameter : SceneParameterBase { QuestId, DeckData }

---

## LifetimeScope 連携方針

AppLifetimeScope（Boot シーン）に登録するもの:
- ISceneManager（UniLabSceneManagerBase の instance）
- アプリ全体で共有するシングルトン（ApiClient 等）

SceneLifetimeScope（各シーン）に登録するもの:
- IScreenManager（ScreenManagerBase の instance）
- IXxxView（各 View の instance を RegisterInstance で登録）
- XxxPresenter（純粋 C# クラスとして Register<T>(Lifetime.Scoped)）

LifetimeScope の親子関係:
- AppLifetimeScope を親に設定することで、SceneLifetimeScope から ISceneManager を参照できる
- シーン破棄時に SceneLifetimeScope も自動 Dispose される

---

## 改修タスク一覧

P0（必須）:
1. IScreenView インターフェース追加
2. ScreenBase を MonoBehaviour ラッパーに改修
3. ScreenPresenterBase 純粋 C# 基底クラス追加
4. IScreenManager インターフェース追加
5. ScreenManagerBase を IScreenManager 実装に改修（履歴スタック・R3 Observable）
6. SceneMainBase を LifetimeScope 委譲に改修
7. ISceneManager インターフェース追加
8. UniLabSceneManagerBase を ISceneManager 実装に改修
9. SceneParameterBase の SetParameter / GetParameter 実装

P1（推奨）:
10. 各クラスに summary コメント追加
11. ScreenManagerBase の画面切り替え EditMode テスト追加
12. SceneMainBase の OnDestroy Dispose テスト追加

