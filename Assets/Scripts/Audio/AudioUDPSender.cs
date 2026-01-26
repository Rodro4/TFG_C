using System.Net.Sockets;
using UnityEngine;

public class AudioUDPSender : MonoBehaviour
{
    UdpClient udp;
    string ip = "192.168.1.148";
    int port = 5004;
    const int sampleRate = 44100;
     
    void Start()
    {
        udp = new UdpClient();
        AudioClip mic = Microphone.Start(null, true, 1, sampleRate);
        GetComponent<AudioSource>().clip = mic;
        GetComponent<AudioSource>().loop = true;
        GetComponent<AudioSource>().Play();
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        byte[] bytes = new byte[data.Length * 2];

        for (int i = 0; i < data.Length; i++)
        {
            short sample = (short)(Mathf.Clamp(data[i], -1f, 1f) * 32767);
            bytes[i * 2] = (byte)(sample & 0xff);
            bytes[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
        }

        udp.Send(bytes, bytes.Length, ip, port);
    }
}
