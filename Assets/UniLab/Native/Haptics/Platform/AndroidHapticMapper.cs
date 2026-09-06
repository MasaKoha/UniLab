namespace UniLab.Native.Haptics.Platform
{
    internal static class AndroidHapticMapper
    {
        public const int ApiOreo = 26;
        public const int ApiQ = 29;
        public const int ApiS = 31;
        public const int DefaultAmplitude = -1;
        public const int EffectClick = 0;
        public const int EffectTick = 2;
        public const int EffectHeavyClick = 5;

        public static AndroidHapticEffectSpec CreateEffect(HapticType type, int apiLevel)
        {
            if (type == HapticType.None)
            {
                return AndroidHapticEffectSpec.None();
            }

            switch (type)
            {
                case HapticType.ImpactLight:
                case HapticType.Selection:
                    return CreateSimpleEffect(apiLevel, EffectTick, 10L);
                case HapticType.ImpactMedium:
                    return CreateSimpleEffect(apiLevel, EffectClick, 20L);
                case HapticType.ImpactHeavy:
                    return CreateSimpleEffect(apiLevel, EffectHeavyClick, 40L);
                case HapticType.Success:
                    return CreateWaveform(apiLevel, new long[] { 0L, 20L, 40L, 20L }, new int[] { 0, DefaultAmplitude, 0, DefaultAmplitude });
                case HapticType.Warning:
                    return CreateOneShot(apiLevel, 30L);
                case HapticType.Error:
                    return CreateWaveform(apiLevel, new long[] { 0L, 40L, 40L, 60L }, new int[] { 0, DefaultAmplitude, 0, DefaultAmplitude });
                default:
                    return AndroidHapticEffectSpec.None();
            }
        }

        public static AndroidHapticEffectSpec CreateOneShot(int apiLevel, long durationMilliseconds)
        {
            var isLegacy = apiLevel < ApiOreo;
            return AndroidHapticEffectSpec.OneShot(durationMilliseconds, DefaultAmplitude, isLegacy);
        }

        private static AndroidHapticEffectSpec CreateSimpleEffect(int apiLevel, int predefinedEffectId, long fallbackDurationMilliseconds)
        {
            if (apiLevel >= ApiQ)
            {
                return AndroidHapticEffectSpec.Predefined(predefinedEffectId);
            }

            return CreateOneShot(apiLevel, fallbackDurationMilliseconds);
        }

        private static AndroidHapticEffectSpec CreateWaveform(int apiLevel, long[] timings, int[] amplitudes)
        {
            var isLegacy = apiLevel < ApiOreo;
            return AndroidHapticEffectSpec.Waveform(timings, amplitudes, isLegacy);
        }
    }
}
