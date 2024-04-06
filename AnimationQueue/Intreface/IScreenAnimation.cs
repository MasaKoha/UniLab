using Cysharp.Threading.Tasks;

namespace UniLab.AnimationQueue
{
    public interface IScreenAnimation
    {
        void Play();
        UniTask Wait();
    }
}