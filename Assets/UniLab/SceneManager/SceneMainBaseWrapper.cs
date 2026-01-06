namespace UniLab.Scene
{
    public abstract class SceneMainBaseWrapper<TParameter> : SceneMainBase where TParameter : SceneParameterBase
    {
        protected new TParameter Parameter => (TParameter)base.Parameter;
    }
}