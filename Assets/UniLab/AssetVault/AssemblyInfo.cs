using System.Runtime.CompilerServices;

// 退避判定（AssetVaultCacheEvictionPolicy）・リトライ間隔（AssetVaultRetryPolicy）等の internal 純ロジックを EditMode テストから検証できるようにする。
[assembly: InternalsVisibleTo("AssetVaultTest")]
