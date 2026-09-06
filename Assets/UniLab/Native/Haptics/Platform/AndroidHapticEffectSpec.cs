namespace UniLab.Native.Haptics.Platform
{
    internal readonly struct AndroidHapticEffectSpec
    {
        public AndroidHapticEffectKind Kind { get; }
        public int PredefinedEffectId { get; }
        public long DurationMilliseconds { get; }
        public int Amplitude { get; }
        public long[] Timings { get; }
        public int[] Amplitudes { get; }

        public AndroidHapticEffectSpec(
            AndroidHapticEffectKind kind,
            int predefinedEffectId,
            long durationMilliseconds,
            int amplitude,
            long[] timings,
            int[] amplitudes)
        {
            Kind = kind;
            PredefinedEffectId = predefinedEffectId;
            DurationMilliseconds = durationMilliseconds;
            Amplitude = amplitude;
            Timings = timings;
            Amplitudes = amplitudes;
        }

        public static AndroidHapticEffectSpec None()
        {
            return new AndroidHapticEffectSpec(AndroidHapticEffectKind.None, 0, 0L, 0, null, null);
        }

        public static AndroidHapticEffectSpec Predefined(int predefinedEffectId)
        {
            return new AndroidHapticEffectSpec(AndroidHapticEffectKind.Predefined, predefinedEffectId, 0L, 0, null, null);
        }

        public static AndroidHapticEffectSpec OneShot(long durationMilliseconds, int amplitude, bool isLegacy)
        {
            var kind = isLegacy ? AndroidHapticEffectKind.LegacyOneShot : AndroidHapticEffectKind.OneShot;
            return new AndroidHapticEffectSpec(kind, 0, durationMilliseconds, amplitude, null, null);
        }

        public static AndroidHapticEffectSpec Waveform(long[] timings, int[] amplitudes, bool isLegacy)
        {
            var kind = isLegacy ? AndroidHapticEffectKind.LegacyWaveform : AndroidHapticEffectKind.Waveform;
            return new AndroidHapticEffectSpec(kind, 0, 0L, 0, timings, amplitudes);
        }
    }
}
