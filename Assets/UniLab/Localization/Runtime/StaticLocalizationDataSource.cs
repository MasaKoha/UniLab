namespace UniLab.Localization
{
    public sealed class StaticLocalizationDataSource : ILocalizationDataSource
    {
        private readonly LocalizationData _data;

        public StaticLocalizationDataSource(LocalizationData data)
        {
            _data = data;
        }

        public LocalizationData Load()
        {
            return _data;
        }
    }
}
