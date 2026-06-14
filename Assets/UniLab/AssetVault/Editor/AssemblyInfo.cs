using System.Runtime.CompilerServices;

// 純粋ロジック（AssetVaultAddressing / AssetVaultDuplicateAddressCollector 等）の internal を
// EditMode テストアセンブリから参照できるようにする。
[assembly: InternalsVisibleTo("AssetVaultTest")]
