using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniLab.Scene
{
    /// <summary>
    /// Provides scene navigation and parameter access for the UniLab scene lifecycle.
    /// </summary>
    public interface ISceneManager
    {
        /// <summary>
        /// Begins loading the next scene. Fire-and-forget; wraps LoadSceneAsync with CancellationToken.None.
        /// </summary>
        void GoToNextScene(SceneParameterBase parameter, bool addToHistory = false);

        /// <summary>
        /// Pops the history stack and returns to the previous scene.
        /// </summary>
        void BackToPreviousScene();

        /// <summary>
        /// Loads the scene described by <paramref name="parameter"/> and awaits the full lifecycle sequence.
        /// </summary>
        UniTask LoadSceneAsync(SceneParameterBase parameter, bool addToHistory = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the current scene's parameter cast to <typeparamref name="TParameter"/>.
        /// Returns null and logs an error if the type does not match.
        /// </summary>
        TParameter GetCurrentSceneParameter<TParameter>() where TParameter : SceneParameterBase;
    }
}
