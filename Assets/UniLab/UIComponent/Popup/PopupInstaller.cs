using VContainer;
using VContainer.Unity;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// Popup 基盤の DI 登録をまとめる Installer。IPopupService と バックキー連携を登録する。
    /// IPopupViewProvider は供給手段（Resources / Addressables / SerializeField）が利用側依存のため、
    /// 呼び出し側の LifetimeScope で別途登録すること。
    /// </summary>
    public sealed class PopupInstaller : IInstaller
    {
        /// <summary>IPopupService を Singleton 登録し、バックキー連携を EntryPoint として起動する。</summary>
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IPopupService, PopupService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PopupBackKeyHandler>();
        }
    }
}
