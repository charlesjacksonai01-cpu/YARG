using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Menu.Persistent;
using YARG.Settings;

namespace YARG.Audio.Unity
{
    /// <summary>
    /// Unity-native audio manager implementation for ARM64 Linux builds.
    /// 
    /// Uses Unity's built-in AudioSource, AudioSettings, and Microphone APIs
    /// instead of the BASS audio library. This eliminates the need for
    /// ARM64 Linux BASS binaries (which don't exist).
    /// 
    /// LIMITATIONS (Proof-of-Concept):
    /// - Pitch and tempo are linked (AudioSource.pitch changes playback speed)
    /// - Latency may be higher than BASS (depends on audio driver)
    /// - No advanced DSP effects (reverb, EQ) unless implemented manually
    /// 
    /// TODO (Future improvements):
    /// - Implement proper pitch/tempo separation via FFT-based time-stretching
    /// - Add DSP effects using AudioFilter
    /// - Optimize latency for rhythm game requirements (<10ms)
    /// </summary>
    public class UnityAudioManager : AudioManager
    {
        private static readonly string[] FORMATS =
        {
            ".ogg", ".mogg", ".wav", ".mp3", ".aiff",
        };

        protected override ReadOnlySpan<string> SupportedFormats => FORMATS;

        private GameObject _audioRoot;
        private AudioListener _masterListener;

        private List<UnityOutputDevice> _outputDevices = new();
        private List<UnityInputDevice> _inputDevices = new();

        public UnityAudioManager()
        {
            YargLogger.LogInfo("Initializing Unity Audio Manager...");

            // Create root GameObject for audio sources
            _audioRoot = new GameObject("UnityAudioManager");
            DontDestroyOnLoad(_audioRoot);

            // Initialize output devices
            InitializeOutputDevices();

            // Initialize input devices (microphones)
            InitializeInputDevices();

            // Set audio settings for low latency
            SetAudioSettings();

            YargLogger.LogInfo("Unity Audio Manager initialized successfully");
            YargLogger.LogFormatInfo("Audio Source: {0} Hz, {1} bits, {2} channels",
                AudioSettings.outputSampleRate, AudioSettings.driverFrequency, AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2);
            YargLogger.LogFormatInfo("DSP Buffer: {0}ms", AudioSettings.dspBufferSize);
        }

        private void InitializeOutputDevices()
        {
            // Unity only exposes the default output device
            // We create a single "Default" device
            var device = new UnityOutputDevice("Default");
            _outputDevices.Add(device);

            YargLogger.LogFormatInfo("Output devices found: {0}", _outputDevices.Count);
        }

        private void InitializeInputDevices()
        {
            // Query available microphones via Unity's Microphone API
            string[] devices = Microphone.devices;

            for (int i = 0; i < devices.Length; i++)
            {
                string name = devices[i];
                if (!string.IsNullOrEmpty(name))
                {
                    var device = new UnityInputDevice(i, name);
                    _inputDevices.Add(device);
                }
            }

            YargLogger.LogFormatInfo("Input devices found: {0}", _inputDevices.Count);
        }

        private void SetAudioSettings()
        {
            // Try to minimize latency
            // AudioSettings.dspBufferSize controls the audio buffer size in milliseconds
            // Lower values = lower latency but more CPU usage
            // Default is typically 25ms; we try 10ms for rhythm game responsiveness
            
            int[] possibleBuffers = { 10, 15, 25, 50 };
            
            foreach (int size in possibleBuffers)
            {
                try
                {
                    AudioSettings.dspBufferSize = size;
                    YargLogger.LogFormatInfo("Set DSP buffer size to {0}ms", size);
                    break;
                }
                catch (System.Exception e)
                {
                    YargLogger.LogFormatInfo("Failed to set DSP buffer to {0}ms: {1}", size, e.Message);
                }
            }

            // Calculate latency values for the base class
            PlaybackLatency = AudioSettings.dspBufferSize;
            MinimumBufferLength = AudioSettings.dspBufferSize;
            MaximumBufferLength = 500; // Unity allows up to 500ms buffer
        }

        protected override StemMixer CreateMixer(string name, float speed, double volume, bool clampStemVolume, bool normalize)
        {
            return new UnityStemMixer(name, this, speed, volume, clampStemVolume, normalize);
        }

        protected override MicDevice GetInputDevice(string name)
        {
            return _inputDevices.FirstOrDefault(d => d.DisplayName == name);
        }

        protected override List<(int id, string name)> GetAllInputDevices()
        {
            return _inputDevices.Select(d => (d.DeviceId, d.DisplayName)).ToList();
        }

        protected override MicDevice CreateInputDevice(int deviceId, string name)
        {
            // Unity handles input devices via Microphone API directly
            // We just return a wrapper around the device ID
            return new UnityInputDevice(deviceId, name);
        }

        protected override OutputChannel CreateOutputChannel(int channelId)
        {
            return new UnityOutputChannel(channelId);
        }

        protected override OutputDevice CreateOutputDevice(int deviceId, string name)
        {
            return new UnityOutputDevice(name);
        }

        protected override List<(int id, string name)> GetAllOutputDevices()
        {
            return _outputDevices.Select(d => (d.DeviceId, d.DisplayName)).ToList();
        }

        protected override int GetOutputChannelCount()
        {
            // Unity exposes speaker mode (mono/stereo/5.1/etc)
            // Return 2 for stereo as default
            return AudioSettings.speakerMode switch
            {
                AudioSpeakerMode.Mono => 1,
                AudioSpeakerMode.Stereo => 2,
                AudioSpeakerMode.Quad => 4,
                AudioSpeakerMode.Surround => 5,
                AudioSpeakerMode.FullSurround => 6,
                _ => 2 // Default to stereo
            };
        }

        protected override OutputDevice GetOutputDevice(string name)
        {
            return _outputDevices.FirstOrDefault(d => d.DisplayName == name);
        }

        protected override void SetMasterVolume(double volume)
        {
            AudioListener.volume = (float)volume;
            YargLogger.LogFormatDebug("Set master volume to {0}", volume);
        }

        protected override void SetBufferLength_Internal(int length)
        {
            try
            {
                AudioSettings.dspBufferSize = length;
                YargLogger.LogFormatInfo("Set buffer length to {0}ms", length);
            }
            catch (System.Exception e)
            {
                YargLogger.LogFormatError("Failed to set buffer length to {0}ms: {1}", length, e.Message);
            }
        }

        protected override void DisposeManagedResources()
        {
            if (_audioRoot != null)
            {
                UnityEngine.Object.Destroy(_audioRoot);
                _audioRoot = null;
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            // Stop any active microphones
            foreach (var device in _inputDevices)
            {
                device.Stop();
            }
        }
    }
}
