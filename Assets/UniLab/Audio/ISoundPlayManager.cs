using UnityEngine;

namespace UniLab.Audio
{
    /// <summary>
    /// BGM / SE / ボイスの再生と音量制御。利用側の LifetimeScope で Singleton 登録する。
    /// </summary>
    public interface ISoundPlayManager
    {
        /// <summary>AudioSource のプールを作り初期音量を適用する。所有者が起動時に一度だけ呼ぶ。</summary>
        void Initialize(AudioCount audioCount, AudioSettings audioSettings);

        /// <summary>マスター音量（0〜1）。</summary>
        void SetMasterVolume(float volume);

        /// <summary>SE 音量（0〜1）。</summary>
        void SetSeVolume(float volume);

        /// <summary>BGM 音量（0〜1）。</summary>
        void SetBgmVolume(float volume);

        /// <summary>ボイス音量（0〜1）。</summary>
        void SetVoiceVolume(float volume);

        /// <summary>空いている SE ソースで再生する。全て使用中なら先頭を上書きする。</summary>
        void PlaySe(AudioClip clip);

        /// <summary>BGM を差し替えて再生する。</summary>
        void PlayBgm(AudioClip clip, bool loop = true);

        /// <summary>空いているボイスソースで再生する。全て使用中なら先頭を上書きする。</summary>
        void PlayVoice(AudioClip clip);
    }
}
