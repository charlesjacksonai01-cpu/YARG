using YARG.Core.Audio;

namespace YARG.Audio.Unity
{
    /// <summary>
    /// Wraps Unity's default output device.
    /// Unity doesn't expose multiple output devices on all platforms,
    /// so this is a simple passthrough wrapper.
    /// </summary>
    public class UnityOutputDevice : OutputDevice
    {
        public UnityOutputDevice(string displayName)
        {
            DisplayName = displayName;
        }
    }
}
