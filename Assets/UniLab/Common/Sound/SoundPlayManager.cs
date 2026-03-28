using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace UniLab.Common.Sound
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

    /// <summary>
    /// Singleton manager for BGM, SE, and voice playback via AudioMixer channels.
    /// Call Initialize() once with AudioCount and AudioSettings before playing audio.
    /// </summary>
    public class SoundPlayManager : SingletonMonoBehaviour<SoundPlayManager>
    {
        [SerializeField] private AudioMixer _audioMixer = null;
        [SerializeField] private AudioMixerGroup _seMixerGroup = null;
        [SerializeField] private AudioMixerGroup _bgmMixerGroup = null;
        [SerializeField] private AudioMixerGroup _voiceMixerGroup = null;

        private AudioSource _bgmSource = null;
        private readonly List<AudioSource> _seSource = new();
        private readonly List<AudioSource> _voiceSource = new();
        private bool _isInitialized = false;

        protected override void OnAwake()
        {
            SetDontDestroyOnLoad();
        }

        public void Initialize(AudioCount audioCount, AudioSettings audioSettings)
        {
            if (_isInitialized)
            {
                return;
            }

            var bgmSource = new GameObject($"BGMSource").AddComponent<AudioSource>();
            bgmSource.transform.SetParent(transform);
            _bgmSource = bgmSource;

            for (var i = 0; i < audioCount.SeCount; i++)
            {
                var source = new GameObject($"SESource_{i}").AddComponent<AudioSource>();
                source.transform.SetParent(transform);
                _seSource.Add(source);
            }

            for (var i = 0; i < audioCount.VoiceCount; i++)
            {
                var source = new GameObject($"VoiceSource_{i}").AddComponent<AudioSource>();
                source.transform.SetParent(transform);
                _voiceSource.Add(source);
            }

            SetMasterVolume(audioSettings.MasterVolume);
            SetBgmVolume(audioSettings.BgmVolume);
            SetSeVolume(audioSettings.SeVolume);
            SetVoiceVolume(audioSettings.VoiceVolume);

            _bgmSource.outputAudioMixerGroup = _bgmMixerGroup;
            _isInitialized = true;
        }

        public void SetMasterVolume(float volume)
        {
            var db = CalculateDb(volume);
            _audioMixer.SetFloat("MasterVolume", db);
        }

        public void SetSeVolume(float volume)
        {
            var db = CalculateDb(volume);
            _audioMixer.SetFloat("SEVolume", db);
        }

        public void SetBgmVolume(float volume)
        {
            var db = CalculateDb(volume);
            _audioMixer.SetFloat("BGMVolume", db);
        }

        public void SetVoiceVolume(float volume)
        {
            var db = CalculateDb(volume);
            _audioMixer.SetFloat("VoiceVolume", db);
        }

        private float CalculateDb(float volume)
        {
            return volume <= 0.01f ? -80f : Mathf.Log10(Mathf.Clamp(volume, 0.01f, 1f)) * 20f;
        }

        public void PlaySe(AudioClip clip)
        {
            foreach (var source in _seSource)
            {
                if (source.isPlaying)
                {
                    continue;
                }

                source.outputAudioMixerGroup = _seMixerGroup;
                source.clip = clip;
                source.Play();
                return;
            }

            // 全て再生中なら最初のAudioSourceで上書き
            if (_seSource.Count <= 0)
            {
                return;
            }

            _seSource[0].outputAudioMixerGroup = _seMixerGroup;
            _seSource[0].clip = clip;
            _seSource[0].Play();
        }

        public void PlayBgm(AudioClip clip, bool loop = true)
        {
            var source = _bgmSource;
            source.outputAudioMixerGroup = _bgmMixerGroup;
            source.clip = clip;
            source.loop = loop;
            source.Play();
        }

        public void PlayVoice(AudioClip clip)
        {
            foreach (var source in _voiceSource)
            {
                if (source.isPlaying)
                {
                    continue;
                }

                source.outputAudioMixerGroup = _voiceMixerGroup;
                source.clip = clip;
                source.Play();
                return;
            }

            // 全て再生中なら最初のAudioSourceで上書き
            if (_voiceSource.Count <= 0)
            {
                return;
            }

            _voiceSource[0].outputAudioMixerGroup = _voiceMixerGroup;
            _voiceSource[0].clip = clip;
            _voiceSource[0].Play();
        }
    }
}