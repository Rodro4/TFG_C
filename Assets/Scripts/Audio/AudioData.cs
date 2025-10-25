namespace RosSharp.RosBridgeClient.MessageTypes.AudioCommon
{
    [System.Serializable]
    public class AudioData : Message
    {
        public const string RosMessageName = "audio_common_msgs/AudioData";
        public byte[] data;

        public AudioData()
        {
            data = new byte[0];
        }

        public AudioData(byte[] data)
        {
            this.data = data;
        }
    }
}
