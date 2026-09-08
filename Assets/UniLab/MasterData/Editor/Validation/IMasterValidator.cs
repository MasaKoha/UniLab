#nullable enable

namespace UniLab.MasterData.Editor.Validation
{
    /// <summary>入力を変更せず診断を追加する検証器の契約。</summary>
    public interface IMasterValidator
    {
        /// <summary>Runner から呼ばれ、修正箇所を共通の結果へ追加する。</summary>
        void Validate(MasterValidationContext context, MasterValidationResult result);
    }
}
