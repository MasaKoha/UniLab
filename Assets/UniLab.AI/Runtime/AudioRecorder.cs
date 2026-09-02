#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>
    /// Unity のミックス後音声を WAV ファイルへ逐次書き出す録音器です。
    /// </summary>
    public sealed class AudioRecorder : IDisposable
    {
        private const int WavHeaderByteCount = 44;
        private const int BitsPerSample = 16;
        private const int BytesPerSample = BitsPerSample / 8;
        private const int PcmFormat = 1;
        private const int DefaultChannelCount = 2;
        private const float MinimumSampleValue = -1.0f;
        private const float MaximumSampleValue = 1.0f;
        private const float PositivePcmScale = 32767.0f;
        private const float NegativePcmScale = 32768.0f;

        private FileStream _fileStream;
        private byte[] _writeBuffer;
        private long _dataByteCount;
        private int _sampleCount;
        private int _sampleRate;
        private int _channelCount;
        private bool _isRecording;

        /// <summary>
        /// 録音中かどうか。
        /// </summary>
        public bool IsRecording
        {
            get
            {
                return _isRecording;
            }
        }

        /// <summary>
        /// 書き出した総サンプル数（1チャンネルあたり）。
        /// </summary>
        public int SampleCount
        {
            get
            {
                return _sampleCount;
            }
        }

        /// <summary>
        /// サンプリングレート。
        /// </summary>
        public int SampleRate
        {
            get
            {
                return _sampleRate;
            }
        }

        /// <summary>
        /// チャンネル数。
        /// </summary>
        public int ChannelCount
        {
            get
            {
                return _channelCount;
            }
        }

        /// <summary>
        /// 録音を開始し、指定パスへ WAV を書き始める。開始できなければ false。
        /// </summary>
        public bool StartRecording(string wavFilePath)
        {
            if (_isRecording)
            {
                StopRecording();
            }

            try
            {
                if (!AudioRenderer.Start())
                {
                    return false;
                }

                _sampleRate = AudioSettings.outputSampleRate;
                _channelCount = ResolveChannelCount(AudioSettings.speakerMode);
                _sampleCount = 0;
                _dataByteCount = 0;

                var directoryPath = Path.GetDirectoryName(wavFilePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                _fileStream = new FileStream(wavFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                WritePlaceholderHeader();
                _isRecording = true;
                return true;
            }
            catch (Exception exception)
            {
                return AbortStartRecording(exception);
            }
        }

        /// <summary>
        /// 1フレーム分の音声を取り出して書き出す。毎フレーム必ず呼ぶこと。
        /// </summary>
        public void PumpFrame()
        {
            if (!_isRecording)
            {
                return;
            }

            var frameSampleCount = AudioRenderer.GetSampleCountForCaptureFrame();
            if (frameSampleCount <= 0)
            {
                return;
            }

            var totalSampleCount = frameSampleCount * _channelCount;
            var samples = new NativeArray<float>(totalSampleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                if (!AudioRenderer.Render(samples))
                {
                    return;
                }

                WriteSamples(samples);
                _sampleCount += frameSampleCount;
            }
            finally
            {
                if (samples.IsCreated)
                {
                    samples.Dispose();
                }
            }
        }

        /// <summary>
        /// 録音を止め、WAV ヘッダを確定する。
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording)
            {
                return;
            }

            _isRecording = false;
            try
            {
                AudioRenderer.Stop();
                FinalizeHeader();
            }
            finally
            {
                CloseFileStream();
            }
        }

        /// <summary>
        /// 録音を停止して使用中のリソースを解放します。
        /// </summary>
        public void Dispose()
        {
            StopRecording();
        }

        private static int ResolveChannelCount(AudioSpeakerMode speakerMode)
        {
            switch (speakerMode)
            {
                case AudioSpeakerMode.Mono:
                    return 1;
                case AudioSpeakerMode.Stereo:
                    return 2;
                case AudioSpeakerMode.Quad:
                    return 4;
                case AudioSpeakerMode.Surround:
                    return 5;
                case AudioSpeakerMode.Mode5point1:
                    return 6;
                case AudioSpeakerMode.Mode7point1:
                    return 8;
                default:
                    return DefaultChannelCount;
            }
        }

        private bool AbortStartRecording(Exception exception)
        {
            UnityEngine.Debug.LogWarning($"[AudioRecorder] 録音の開始に失敗しました。 {exception.GetType().Name}: {exception.Message}");
            try
            {
                AudioRenderer.Stop();
            }
            catch (Exception stopException)
            {
                UnityEngine.Debug.LogWarning($"[AudioRecorder] 開始失敗後の停止に失敗しました。 {stopException.GetType().Name}: {stopException.Message}");
            }
            finally
            {
                CloseFileStream();
            }

            return false;
        }

        private void WritePlaceholderHeader()
        {
            var header = new byte[WavHeaderByteCount];
            WriteHeader(header, 0);
            _fileStream.Write(header, 0, header.Length);
        }

        private void FinalizeHeader()
        {
            if (_fileStream == null)
            {
                return;
            }

            var header = new byte[WavHeaderByteCount];
            WriteHeader(header, _dataByteCount);
            _fileStream.Seek(0, SeekOrigin.Begin);
            _fileStream.Write(header, 0, header.Length);
            _fileStream.Flush();
        }

        private void WriteHeader(byte[] header, long dataByteCount)
        {
            var blockAlign = _channelCount * BytesPerSample;
            var byteRate = _sampleRate * blockAlign;

            WriteAscii(header, 0, "RIFF");
            WriteUInt32LittleEndian(header, 4, 36U + (uint)dataByteCount);
            WriteAscii(header, 8, "WAVE");
            WriteAscii(header, 12, "fmt ");
            WriteUInt32LittleEndian(header, 16, 16U);
            WriteUInt16LittleEndian(header, 20, PcmFormat);
            WriteUInt16LittleEndian(header, 22, _channelCount);
            WriteUInt32LittleEndian(header, 24, (uint)_sampleRate);
            WriteUInt32LittleEndian(header, 28, (uint)byteRate);
            WriteUInt16LittleEndian(header, 32, blockAlign);
            WriteUInt16LittleEndian(header, 34, BitsPerSample);
            WriteAscii(header, 36, "data");
            WriteUInt32LittleEndian(header, 40, (uint)dataByteCount);
        }

        private void WriteSamples(NativeArray<float> samples)
        {
            var requiredByteCount = samples.Length * BytesPerSample;
            EnsureWriteBufferSize(requiredByteCount);

            for (var sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
            {
                var pcmSample = ConvertToPcm16(samples[sampleIndex]);
                var byteIndex = sampleIndex * BytesPerSample;
                _writeBuffer[byteIndex] = (byte)(pcmSample & 0xFF);
                _writeBuffer[byteIndex + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }

            _fileStream.Write(_writeBuffer, 0, requiredByteCount);
            _dataByteCount += requiredByteCount;
        }

        private void EnsureWriteBufferSize(int requiredByteCount)
        {
            if (_writeBuffer != null && _writeBuffer.Length >= requiredByteCount)
            {
                return;
            }

            _writeBuffer = new byte[requiredByteCount];
        }

        private static short ConvertToPcm16(float sample)
        {
            var clampedSample = Mathf.Clamp(sample, MinimumSampleValue, MaximumSampleValue);
            if (clampedSample < 0.0f)
            {
                return (short)(clampedSample * NegativePcmScale);
            }

            return (short)(clampedSample * PositivePcmScale);
        }

        private static void WriteAscii(byte[] bytes, int offset, string value)
        {
            for (var characterIndex = 0; characterIndex < value.Length; characterIndex++)
            {
                bytes[offset + characterIndex] = (byte)value[characterIndex];
            }
        }

        private static void WriteUInt16LittleEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value & 0xFF);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteUInt32LittleEndian(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value & 0xFF);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
            bytes[offset + 2] = (byte)((value >> 16) & 0xFF);
            bytes[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private void CloseFileStream()
        {
            if (_fileStream == null)
            {
                return;
            }

            _fileStream.Dispose();
            _fileStream = null;
        }
    }
}
#endif
