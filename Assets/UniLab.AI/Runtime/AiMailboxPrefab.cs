#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>Prefab の型付き参照を保持し、実行時のコンポーネント検索を不要にします。</summary>
    internal sealed class AiMailboxPrefab : ScriptableObject
    {
        [SerializeField] private AiMailboxServer _server;

        internal AiMailboxServer Server => _server;
    }
}
#endif
