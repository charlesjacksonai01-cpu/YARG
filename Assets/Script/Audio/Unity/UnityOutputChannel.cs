using YARG.Core.Audio;

namespace YARG.Audio.Unity
{
    /// <summary>
    /// Output channel wrapper for Unity's audio system.
    /// 
    /// Unity's audio system doesn't expose individual hardware channels
    /// directly — routing is handled through Audio Mixer groups and
    /// AudioSource.pan/spatialBlend settings. This is a simple passthrough
    /// wrapper to satisfy the interface.
    /// </summary>
    public class UnityOutputChannel : OutputChannel
    {
        public UnityOutputChannel(int channelId)
        {
            ChannelId = channelId;
        }
    }
}
