#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace UniLab.MasterData.Editor.Validation
{
    /// <summary>検証器の順序を保ち、全入力の診断を集約する。</summary>
    public sealed class MasterValidationRunner
    {
        private readonly IReadOnlyList<IMasterValidator> _validators;

        /// <summary>構造検証を値検証より前に登録する。</summary>
        public MasterValidationRunner(IEnumerable<IMasterValidator> validators)
        {
            _validators = validators.ToArray();
        }

        /// <summary>参照先を含め、前段の検証が全件終わってから次の検証へ進む。</summary>
        public MasterValidationResult Validate(IReadOnlyList<MasterValidationContext> contexts, MasterValidationResult? result = null)
        {
            result ??= new MasterValidationResult();
            foreach (var validator in _validators)
            {
                foreach (var context in contexts)
                {
                    validator.Validate(context, result);
                }
            }
            return result;
        }
    }
}
