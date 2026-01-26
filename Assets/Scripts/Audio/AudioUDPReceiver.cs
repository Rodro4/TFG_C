using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class AudioUDPReceiver : MonoBehaviour
{
    UdpClient udp;
    AudioSource audioSource;
    const int sampleRate = 44100;
    const int channels = 2;

    void Start()
    {
        udp = new UdpClient(5002);
        audioSource = GetComponent<AudioSource>();

        audioSource.clip = AudioClip.Create("RemoteAudio", sampleRate, channels, sampleRate, true, OnAudioRead);
        audioSource.loop = true;
        audioSource.Play();
    }

    void OnAudioRead(float[] data)
    {
        if (udp.Available < data.Length * 2) return;

        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        byte[] bytes = udp.Receive(ref ep);

        for (int i = 0; i < data.Length; i++)
        {
            short sample = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            data[i] = sample / 32768f;
        }
    }
}
