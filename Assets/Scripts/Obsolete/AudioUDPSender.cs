using RosSharp.RosBridgeClient;
using System;
using System.Net.Sockets;
using UnityEngine;

public class AudioUDPSender : MonoBehaviour
{
    private UdpClient udp;
    private string ip;
    private int port = 5004;
    private const int sampleRate = 44100;

    public RosConnector rosConnector;

    void Start()
    {
        udp = new UdpClient();

        string rosUrl = rosConnector.RosBridgeServerUrl; // ws://192.168.1.147:9090
        Uri uri = new Uri(rosUrl);
        ip = uri.Host;
        Debug.Log("IP: " + ip);

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
