using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.UI.Transition
{
    /// <summary>
    /// Provides fade-in/fade-out transitions for scene changes.
    /// </summary>
    public interface ISceneTransition
    {
        /// <summary>
        /// Fades the screen in (alpha 1 to 0), making the scene visible.
        /// </summary>
        UniTask FadeInAsync(float duration = 0.3f, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fades the screen out (alpha 0 to 1), darkening the scene before a load.
        /// </summary>
        UniTask FadeOutAsync(float duration = 0.3f, CancellationToken cancellationToken = default);
    }
}
