using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioReceiverMobile : MonoBehaviour
{
    [Header("Configuraci�n")]
    public int port = 5004;
    private int sampleRate = 16000;
    public bool isDeafened = false;

    private UdpClient udp;
    private ConcurrentQueue<float> audioQueue = new ConcurrentQueue<float>();

    void Awake()
    {
        var config = AudioSettings.GetConfiguration();
        config.sampleRate = sampleRate;
        AudioSettings.Reset(config);
    }

    void Start()
    {
        Application.runInBackground = true;
        udp = new UdpClient(port);
        StartReceiving();

        var audioSource = GetComponent<AudioSource>();
        if (!audioSource.isPlaying) audioSource.Play();
    }

    async void StartReceiving()
    {
        while (true)
        {
            try
            {
                var result = await udp.ReceiveAsync();
                if (result.Buffer.Length == 0 || isDeafened) continue;

                int samples = result.Buffer.Length / 2;
                for (int i = 0; i < samples; i++)
                {
                    short pcm = (short)(result.Buffer[i * 2] | (result.Buffer[i * 2 + 1] << 8));
                    audioQueue.Enqueue(pcm / 32768f);
                }
            }
            catch (Exception) { break; }
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;
            if (audioQueue.TryDequeue(out sample))
            {
                for (int c = 0; c < channels; c++) data[i + c] = sample;
            }
            else
            {
                for (int c = 0; c < channels; c++) data[i + c] = 0f;
            }
        }
    }

    void OnApplicationQuit() => udp?.Close();
}