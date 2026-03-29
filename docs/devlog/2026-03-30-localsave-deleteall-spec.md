# LocalSave.DeleteAll() Registry ベース修正 — 実装仕様

## 問題

`LocalSave.DeleteAll()` が `PlayerPrefs.DeleteAll()` を呼ぶため、
LocalSave が管理していない PlayerPrefs エントリ（他システムのデータ等）も削除してしまう。

## 方針

- **Editor**: registry（`KeyListKey` に CSV 保存されたキー一覧）に登録されたキーのみ削除し、registry 自体も削除する
- **Runtime**: `PlayerPrefs.DeleteAll()` を維持（registry が存在しないため）

## 変更対象ファイル

1. `Assets/UniLab/Persistence/LocalSave.cs`
2. `Assets/UniLab/Persistence/Editor/LocalSaveEditorWindow.cs`
3. `Assets/Editor/Tests/LocalSave/LocalSaveTest.cs`

---

## 1. LocalSave.cs の変更

### `DeleteAll()` メソッド

**変更前:**
```csharp
public static void DeleteAll()
{
    PlayerPrefs.DeleteAll();
    PlayerPrefs.Save();
}
```

**変更後:**
```csharp
/// <summary>
/// Deletes all LocalSave data.
/// In the Editor, only keys registered by LocalSave are removed so that
/// PlayerPrefs entries from other systems are preserved.
/// In runtime builds, PlayerPrefs.DeleteAll() is used as no registry is available.
/// </summary>
public static void DeleteAll()
{
#if UNITY_EDITOR
    foreach (var key in GetAllKeysInEditor())
    {
        PlayerPrefs.DeleteKey(key);
    }
    PlayerPrefs.DeleteKey(KeyListKey);
    PlayerPrefs.Save();
#else
    PlayerPrefs.DeleteAll();
    PlayerPrefs.Save();
#endif
}
```

### 既存コードで修正が必要な別のバグ

`Delete<T>()` は PlayerPrefs からキーを削除した後に `RegisterKeyInEditor(key)` を呼んでいる。
これはキーが既に登録済みの場合は no-op だが、意図が不明瞭。
このメソッドの変更は今回のスコープ外とし、修正しない。

---

## 2. LocalSaveEditorWindow.cs の変更

"Delete All" ダイアログの文言を、Editor での新しい動作（LocalSave 管理キーのみ削除）に合わせて更新する。

**変更前:**
```csharp
if (EditorUtility.DisplayDialog("Confirm", "Delete all PlayerPrefs data? This clears all LocalSave entries and any other PlayerPrefs keys.", "Delete", "Cancel"))
```

**変更後:**
```csharp
if (EditorUtility.DisplayDialog("Confirm", "Delete all LocalSave data? Only keys registered by LocalSave will be removed.", "Delete", "Cancel"))
```

---

## 3. LocalSaveTest.cs の変更

### 追加テスト

Editor モードでのみ動作するテストを追加。
非 LocalSave の PlayerPrefs キーが `DeleteAll()` で削除されないことを確認する。

```csharp
[Test]
public void DeleteAll_InEditor_PreservesNonLocalSavePlayerPrefsKeys()
{
    // Simulate a PlayerPrefs key set by another system (not LocalSave)
    const string externalKey = "external_system_key";
    PlayerPrefs.SetString(externalKey, "external_value");
    PlayerPrefs.Save();

    LocalSave.Save(new SampleData { Score = 1 });
    LocalSave.DeleteAll();

    Assert.IsFalse(PlayerPrefs.HasKey(typeof(SampleData).FullName),
        "LocalSave-managed key should be deleted.");
    Assert.IsTrue(PlayerPrefs.HasKey(externalKey),
        "Non-LocalSave PlayerPrefs keys must not be deleted by DeleteAll().");

    // Cleanup
    PlayerPrefs.DeleteKey(externalKey);
    PlayerPrefs.Save();
}
```

テストクラスには既存の private クラス `SampleData` が存在するためそのまま流用する。

---

## コーディング規約

- ブロック namespace (`namespace UniLab.Persistence { ... }`)
- `{}` は if/else/for/foreach 全てに付ける
- public メンバーには `/// <summary>` を付ける
- `#if UNITY_EDITOR` ブロックは既存パターンに合わせる
- 変数名の省略形は禁止
