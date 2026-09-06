#if UNITY_ANDROID
using UniLab.Native.Haptics.Platform.Interface;
using UnityEngine;

namespace UniLab.Native.Haptics.Platform
{
    /// <summary>
    /// Android のネイティブハプティクス実装。
    /// </summary>
    internal sealed class AndroidHaptic : IPlatformHaptic
    {
        /// <summary>
        /// 意味的なハプティクスを再生する。
        /// </summary>
        /// <param name="type">触覚フィードバックの種類。</param>
        public void Play(HapticType type)
        {
            if (type == HapticType.None)
            {
                return;
            }

            var apiLevel = GetApiLevel();
            var effectSpec = AndroidHapticMapper.CreateEffect(type, apiLevel);
            PlayEffect(effectSpec, apiLevel);
        }

        /// <summary>
        /// 生の一発振動を再生する。
        /// </summary>
        /// <param name="milliseconds">振動時間。</param>
        public void Vibrate(int milliseconds)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            var apiLevel = GetApiLevel();
            var effectSpec = AndroidHapticMapper.CreateOneShot(apiLevel, milliseconds);
            PlayEffect(effectSpec, apiLevel);
        }

        private static int GetApiLevel()
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            return version.GetStatic<int>("SDK_INT");
        }

        private static void PlayEffect(AndroidHapticEffectSpec effectSpec, int apiLevel)
        {
            if (effectSpec.Kind == AndroidHapticEffectKind.None)
            {
                return;
            }

            using var vibrator = GetVibrator(apiLevel);
            if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
            {
                return;
            }

            switch (effectSpec.Kind)
            {
                case AndroidHapticEffectKind.Predefined:
                    PlayPredefined(vibrator, effectSpec.PredefinedEffectId);
                    break;
                case AndroidHapticEffectKind.OneShot:
                    PlayOneShot(vibrator, effectSpec.DurationMilliseconds, effectSpec.Amplitude);
                    break;
                case AndroidHapticEffectKind.Waveform:
                    PlayWaveform(vibrator, effectSpec.Timings, effectSpec.Amplitudes);
                    break;
                case AndroidHapticEffectKind.LegacyOneShot:
                    vibrator.Call("vibrate", effectSpec.DurationMilliseconds);
                    break;
                case AndroidHapticEffectKind.LegacyWaveform:
                    vibrator.Call("vibrate", effectSpec.Timings, -1);
                    break;
            }
        }

        private static AndroidJavaObject GetVibrator(int apiLevel)
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (apiLevel >= AndroidHapticMapper.ApiS)
            {
                using var vibratorManager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager");
                return vibratorManager.Call<AndroidJavaObject>("getDefaultVibrator");
            }

            return activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }

        private static void PlayPredefined(AndroidJavaObject vibrator, int effectId)
        {
            using var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            using var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createPredefined", effectId);
            vibrator.Call("vibrate", effect);
        }

        private static void PlayOneShot(AndroidJavaObject vibrator, long milliseconds, int amplitude)
        {
            using var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            using var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude);
            vibrator.Call("vibrate", effect);
        }

        private static void PlayWaveform(AndroidJavaObject vibrator, long[] timings, int[] amplitudes)
        {
            using var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            using var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1);
            vibrator.Call("vibrate", effect);
        }
    }
}
#endif
