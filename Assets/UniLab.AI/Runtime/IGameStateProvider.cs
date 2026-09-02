#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace UniLab.AI
{
    /// <summary>
    /// ゲーム固有の状態を平坦なキー値へ落として受け渡すための入口です。
    /// スナップショット側がゲーム本体へ依存しないまま診断情報を増やせるように分離します。
    /// </summary>
    public interface IGameStateProvider
    {
        /// <summary>
        /// JSON 化しやすい平坦な状態を返します。
        /// UniLab.AI 側で辞書を直接扱えないため、ここで値域を絞っておく前提です。
        /// </summary>
        IReadOnlyDictionary<string, object> GetState();
    }
}
#endif
