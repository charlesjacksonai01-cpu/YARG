using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.Unity
{
    /// <summary>
    /// Unity-native stem mixer implementation.
    /// 
    /// Uses one AudioSource per stem (song, drums, bass, etc.) to allow
    /// independent volume and pitch control per stem.
    /// 
    /// PROOF-OF-CONCEPT: Pitch and tempo are linked (AudioSource.pitch).
    /// For a full implementation, we'd need FFT-based time-stretching to
    /// separate pitch from tempo like BASS.FX does.
    /// </summary>
    public class UnityStemMixer : StemMixer
    {
        private class StemAudioSource
        {
            public SongStem Stem;
            public AudioSource Source;
            public AudioClip Clip;
        }

        private List<StemAudioSource> _stems = new();
        private float _speed;
        private float _volume;
        private float _lastPosition;
        private GameObject _audioRoot;

        public UnityStemMixer(string name, AudioManager manager, float speed, double volume, bool clampStemVolume, bool normalize)
            : base(name, manager, clampStemVolume)
        {
            // Create root GameObject for this mixer's audio sources
            _audioRoot = new GameObject($"StemMixer_{name}");
            _speed = speed;
            _volume = (float)volume;
        }

        public override event Action SongEnd
        {
            add { _songEnd += value; }
            remove { _songEnd -= value; }
        }

        protected override int Play_Internal()
        {
            foreach (var stem in _stems)
            {
                stem.Source.Play();
            }
            return 0;
        }

        protected override void FadeIn_Internal(double maxVolume, double duration)
        {
            // TODO: Implement crossfade
            _volume = (float)maxVolume;
            foreach (var stem in _stems)
            {
                stem.Source.volume = _volume;
            }
        }

        protected override void FadeOut_Internal(double duration)
        {
            // TODO: Implement crossfade
            _volume = 0f;
            foreach (var stem in _stems)
            {
                stem.Source.volume = 0f;
            }
        }

        protected override int Pause_Internal()
        {
            foreach (var stem in _stems)
            {
                stem.Source.Pause();
            }
            return 0;
        }

        protected override double GetPosition_Internal()
        {
            // Return the position of the first stem
            if (_stems.Count == 0)
                return 0;

            return _stems[0].Source.time;
        }

        protected override double GetVolume_Internal()
        {
            return _volume;
        }

        protected override void SetPosition_Internal(double position)
        {
            foreach (var stem in _stems)
            {
                stem.Source.time = (float)position;
            }
        }

        protected override void SetVolume_Internal(double volume)
        {
            _volume = (float)volume;
            foreach (var stem in _stems)
            {
                stem.Source.volume = _volume;
            }
        }

        protected override int GetSampleData_Internal(float[] buffer)
        {
            // Get audio samples from the first stem
            if (_stems.Count == 0)
                return 0;

            var stem = _stems[0];
            float samplesRead = stem.Clip.GetData(buffer, (int)stem.Source.timeSamples);
            return (int)samplesRead;
        }

        protected override int GetFFTData_Internal(float[] buffer, int fftSize, bool complex)
        {
            // Unity doesn't expose raw FFT data easily
            // We'd need an AudioListener.GetSpectrumData or custom DSP script
            // For now, return 0
            return 0;
        }

        protected override int GetLevel_Internal(float[] level)
        {
            // Return volume level
            level[0] = _volume;
            level[1] = _volume;
            return 2;
        }

        protected override void SetSpeed_Internal(float speed, bool shiftPitch)
        {
            // PROOF-OF-CONCEPT: AudioSource.pitch affects BOTH pitch and speed
            // shiftPitch=true: Use pitch to change speed (changes pitch)
            // shiftPitch=false: TODO - implement time-stretching to maintain pitch
            
            _speed = speed;

            if (shiftPitch)
            {
                // Simple approach: use pitch to change speed (changes pitch)
                foreach (var stem in _stems)
                {
                    stem.Source.pitch = _speed;
                }
            }
            else
            {
                // TODO: Implement proper time-stretching (maintain pitch while changing speed)
                // For now, just change playback rate without pitch correction
                // This is not ideal but works for PoC
                foreach (var stem in _stems)
                {
                    stem.Source.pitch = _speed;
                    YargLogger.LogDebug("Note: shiftPitch=false not fully implemented. Pitch will change with speed.");
                }
            }
        }

        protected override bool AddChannels_Internal(Stream stream, params StemInfo[] stemInfos)
        {
            // Load audio clips from streams and create AudioSources
            foreach (var stemInfo in stemInfos)
            {
                if (!LoadStemFromStream(stream, stemInfo))
                {
                    return false;
                }
            }
            return true;
        }

        private bool LoadStemFromStream(Stream stream, StemInfo info)
        {
            // Load audio clip from stream
            // Unity's AudioClip.LoadAudioData is available in Unity 2021+
            
            var audioClip = AudioClip.Create(
                $"Stem_{info.Stem}",
                0, // Auto-calculate length
                2, // Stereo
                44100,
                true
            );

            // Read stream into buffer and load
            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);

            bool loaded = audioClip.LoadAudioData(buffer);
            if (!loaded)
            {
                YargLogger.LogFormatError("Failed to load audio clip for stem {0}", info.Stem);
                return false;
            }

            // Create AudioSource
            var source = _audioRoot.AddComponent<AudioSource>();
            source.clip = audioClip;
            source.spatialBlend = 0f; // 2D audio
            source.volume = _volume;
            source.playOnAwake = false;

            // Apply speed
            source.pitch = _speed;

            // Store stem
            var stemAudioSource = new StemAudioSource
            {
                Stem = info.Stem,
                Source = source,
                Clip = audioClip
            };

            _stems.Add(stemAudioSource);
            _length = audioClip.length;

            YargLogger.LogFormatInfo("Loaded stem {0} ({1}s)", info.Stem, audioClip.length);
            return true;
        }

        protected override bool RemoveChannel_Internal(SongStem stemToRemove)
        {
            for (int i = 0; i < _stems.Count; i++)
            {
                if (_stems[i].Stem == stemToRemove)
                {
                    UnityEngine.Object.Destroy(_stems[i].Source);
                    UnityEngine.Object.Destroy(_stems[i].Clip);
                    _stems.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        protected override void SetBufferLength_Internal(int length)
        {
            // Audio buffer size is controlled by AudioManager
        }

        protected override void SetOutputChannel_Internal(OutputChannel? channel)
        {
            // TODO: Implement output channel routing via Audio Mixer
        }

        protected override void SetOutputDevice_Internal(OutputDevice device)
        {
            // Unity doesn't expose multiple output devices directly
        }

        protected override void DisposeManagedResources()
        {
            foreach (var stem in _stems)
            {
                if (stem.Source != null)
                {
                    UnityEngine.Object.Destroy(stem.Source);
                }
                if (stem.Clip != null)
                {
                    UnityEngine.Object.Destroy(stem.Clip);
                }
            }
            _stems.Clear();
        }

        protected override void DisposeUnmanagedResources()
        {
            // Stop playback
            foreach (var stem in _stems)
            {
                stem.Source.Stop();
            }
        }
    }
}
