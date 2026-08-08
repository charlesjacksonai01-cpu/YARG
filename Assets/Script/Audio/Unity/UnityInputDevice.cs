using System;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.Unity
{
    /// <summary>
    /// Wraps Unity's Microphone API for input device handling.
    /// Used for guitar controller microphone input and similar features.
    /// </summary>
    public class UnityInputDevice : MicDevice
    {
        private readonly int _deviceId;
        private bool _isRecording;

        public UnityInputDevice(int deviceId, string displayName)
        {
            DeviceId = deviceId;
            DisplayName = displayName;
        }

        public override event Action<string[]> SampleDataAvailable
        {
            add { _sampleDataAvailable += value; }
            remove { _sampleDataAvailable -= value; }
        }

        protected override bool StartRecording_Internal(int frequency, int channels, int bufferLength)
        {
            if (_isRecording)
            {
                Stop();
            }

            try
            {
                // Start recording on this device
                Microphone.Start(DisplayName, true, bufferLength, frequency);

                // Wait for microphone to be ready
                int timeout = 1000;
                while (Microphone.GetPosition(DisplayName) == 0 && timeout > 0)
                {
                    System.Threading.Thread.Sleep(10);
                    timeout -= 10;
                }

                if (timeout <= 0)
                {
                    YargLogger.LogFormatError("Timed out waiting for microphone {0}", DisplayName);
                    return false;
                }

                _isRecording = true;
                YargLogger.LogFormatInfo("Started recording on {0} at {1}Hz", DisplayName, frequency);
                return true;
            }
            catch (Exception e)
            {
                YargLogger.LogFormatError("Failed to start recording on {0}: {1}", DisplayName, e.Message);
                return false;
            }
        }

        protected override void Stop_Internal()
        {
            if (_isRecording)
            {
                try
                {
                    Microphone.End(DisplayName);
                    _isRecording = false;
                    YargLogger.LogFormatInfo("Stopped recording on {0}", DisplayName);
                }
                catch (Exception e)
                {
                    YargLogger.LogFormatError("Failed to stop recording on {0}: {1}", DisplayName, e.Message);
                }
            }
        }

        protected override float[] GetSamples_Internal(int sampleCount)
        {
            if (!_isRecording)
                return new float[0];

            // Read samples from microphone
            float[] samples = new float[sampleCount];
            int position = Microphone.GetPosition(DisplayName);

            if (position == 0)
                return samples;

            // Microphone.GetData returns all recorded samples
            float[] allData = new float[Microphone.GetDeviceCaps(DisplayName).maxFrequency * (int)Microphone.GetDeviceCaps(DisplayName).maxDuration];
            Microphone.GetData(allData, 0);

            // Copy the requested number of samples
            int count = Math.Min(sampleCount, Math.Min(allData.Length - position, allData.Length));
            Array.Copy(allData, position, samples, 0, count);

            return samples;
        }

        protected override void DisposeManagedResources()
        {
            Stop();
        }

        protected override void DisposeUnmanagedResources()
        {
            // Nothing to clean up
        }
    }
}
