#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace UniLab.Audio
{
    /// <summary>Initial volume levels (0–1) passed to SoundPlayManager.Initialize().</summary>
    public class AudioSettings
    {
        /// <summary>Initial SE channel volume (0–1).</summary>
        public float SeVolume = 1.0f;
        /// <summary>Initial BGM channel volume (0–1).</summary>
        public float BgmVolume = 1.0f;
        /// <summary>Initial voice channel volume (0–1).</summary>
        public float VoiceVolume = 1.0f;
        /// <summary>Initial master channel volume (0–1).</summary>
        public float MasterVolume = 1.0f;
    }

    /// <summary>Number of pooled AudioSource instances created per channel at initialization.</summary>
    public class AudioCount
    {
        /// <summary>Number of simultaneously playable SE sources.</summary>
        public int SeCount = 8;
        /// <summary>Number of simultaneously playable voice sources.</summary>
        public int VoiceCount = 10;
    }

    /// <summary>常駐する音源プール。ミキサー未設定時は線形音量で再生する。</summary>
    public class SoundPlayManager : MonoBehaviour, ISoundPlayManager
    {
        /// <summary>未設定なら AudioSource.volume を使用する。</summary>
        [SerializeField] private AudioMixer? _audioMixer = null;
        /// <summary>未設定ならミキサーへルーティングせず、ソース音量を使う。</summary>
        [SerializeField] private AudioMixerGroup? _seMixerGroup = null;
        /// <summary>未設定ならミキサーへルーティングせず、ソース音量を使う。</summary>
        [SerializeField] private AudioMixerGroup? _bgmMixerGroup = null;
        /// <summary>未設定ならミキサーへルーティングせず、ソース音量を使う。</summary>
        [SerializeField] private AudioMixerGroup? _voiceMixerGroup = null;
        [SerializeField] private AudioSource _audioSourcePrefab = null!;
        [SerializeField] private string _masterParameter = "MasterVolume";
        [SerializeField] private string _bgmParameter = "BGMVolume";
        [SerializeField] private string _seParameter = "SEVolume";
        [SerializeField] private string _voiceParameter = "VoiceVolume";
        private AudioSource _bgmSource = null!;
        private readonly List<AudioSource> _seSources = new();
        private readonly List<AudioSource> _voiceSources = new();
        private float _masterVolume = 1f;
        private float _bgmVolume = 1f;
        private float _seVolume = 1f;
        private float _voiceVolume = 1f;
        private bool _isInitialized;

        /// <summary>所有者が起動時に音源プールを生成する。</summary>
        public void Initialize(AudioCount audioCount, AudioSettings audioSettings)
        {
            if (_isInitialized)
            {
                return;
            }
            _bgmSource = Instantiate(_audioSourcePrefab, transform);
            _bgmSource.outputAudioMixerGroup = _bgmMixerGroup;
            CreateSources(_seSources, audioCount.SeCount, _seMixerGroup);
            CreateSources(_voiceSources, audioCount.VoiceCount, _voiceMixerGroup);
            _isInitialized = true;
            SetMasterVolume(audioSettings.MasterVolume);
            SetBgmVolume(audioSettings.BgmVolume);
            SetSeVolume(audioSettings.SeVolume);
            SetVoiceVolume(audioSettings.VoiceVolume);
        }

        private void CreateSources(List<AudioSource> sources, int count, AudioMixerGroup? group)
        {
            for (var index = 0; index < count; index++)
            {
                var source = Instantiate(_audioSourcePrefab, transform);
                source.outputAudioMixerGroup = group;
                sources.Add(source);
            }
        }

        /// <summary>マスター音量を設定する。</summary>
        public void SetMasterVolume(float volume) { _masterVolume = Mathf.Clamp01(volume); ApplyVolumes(); }
        /// <summary>BGM 音量を設定する。</summary>
        public void SetBgmVolume(float volume) { _bgmVolume = Mathf.Clamp01(volume); ApplyVolumes(); }
        /// <summary>SE 音量を設定する。</summary>
        public void SetSeVolume(float volume) { _seVolume = Mathf.Clamp01(volume); ApplyVolumes(); }
        /// <summary>ボイス音量を設定する。</summary>
        public void SetVoiceVolume(float volume) { _voiceVolume = Mathf.Clamp01(volume); ApplyVolumes(); }

        private void ApplyVolumes()
        {
            if (!_isInitialized)
            {
                return;
            }
            // perf: フェード中も配列を作らず既存ソースへ適用する。
            var masterUsesMixer = SetMixerVolume(_masterParameter, _masterVolume);
            var masterGain = masterUsesMixer ? 1f : _masterVolume;
            _bgmSource.volume = masterGain * (SetMixerVolume(_bgmParameter, _bgmVolume) ? 1f : _bgmVolume);
            ApplySourceVolumes(_seSources, masterGain * (SetMixerVolume(_seParameter, _seVolume) ? 1f : _seVolume));
            if (_voiceSources.Count > 0)
            {
                ApplySourceVolumes(_voiceSources, masterGain * (SetMixerVolume(_voiceParameter, _voiceVolume) ? 1f : _voiceVolume));
            }
        }

        private bool SetMixerVolume(string parameter, float volume)
        {
            const float SilenceDecibels = -80f;
            const float AmplitudeDecibelFactor = 20f;
            return _audioMixer != null && _audioMixer.SetFloat(parameter,
                volume <= 0f ? SilenceDecibels : Mathf.Max(SilenceDecibels, AmplitudeDecibelFactor * Mathf.Log10(volume)));
        }

        private static void ApplySourceVolumes(List<AudioSource> sources, float volume)
        {
            foreach (var source in sources)
            {
                source.volume = volume;
            }
        }

        /// <summary>空きソース、または最も古い音を置換して SE を鳴らす。</summary>
        public void PlaySe(AudioClip clip) => PlayPooled(_seSources, clip);
        /// <summary>ボイスを鳴らす。</summary>
        public void PlayVoice(AudioClip clip) => PlayPooled(_voiceSources, clip);
        private static void PlayPooled(List<AudioSource> sources, AudioClip clip)
        {
            if (sources.Count == 0)
            {
                return;
            }
            var selectedIndex = 0;
            for (var index = 0; index < sources.Count; index++)
            {
                if (!sources[index].isPlaying)
                {
                    selectedIndex = index;
                    break;
                }
            }
            var source = sources[selectedIndex];
            sources.RemoveAt(selectedIndex);
            sources.Add(source);
            source.Stop();
            source.clip = clip;
            source.Play();
        }

        /// <summary>曲を差し替えて再生する。</summary>
        public void PlayBgm(AudioClip clip, bool loop = true)
        {
            _bgmSource.Stop();
            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            _bgmSource.Play();
        }
        /// <summary>所有者が Addressables を解放する前に再生参照を外す。</summary>
        public void StopBgm()
        {
            if (!_isInitialized)
            {
                return;
            }
            _bgmSource.Stop();
            _bgmSource.clip = null;
        }
        /// <summary>終了時に SE の再生参照を外す。</summary>
        public void StopSoundEffects()
        {
            foreach (var source in _seSources)
            {
                source.Stop();
                source.clip = null;
            }
        }
    }
}
