namespace UniLab.Persistence
{
    /// <summary>
    /// LocalSave / EncryptedLocalStorage のシリアライズ方式を差し替えるための抽象。
    /// 実装を付け替えることで JSON・MessagePack などを切り替えられる。
    /// 文字列ベース（JSON）とバイナリベース（MessagePack）を統一的に扱うため、
    /// 直列化結果は byte[] で受け渡し、保存層が Base64 化・暗号化して永続化する。
    /// </summary>
    public interface ILocalSaveSerializer
    {
        /// <summary>
        /// データをバイト列へ直列化する。保存層（LocalSave 等）が呼び、結果を永続化する。
        /// </summary>
        byte[] Serialize<TData>(TData data);

        /// <summary>
        /// バイト列をデータへ復元する。直列化時と同じ実装で呼ぶ必要がある
        /// （方式が異なると復元に失敗する）。
        /// </summary>
        TData Deserialize<TData>(byte[] bytes);
    }
}
