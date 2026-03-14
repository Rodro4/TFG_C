using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioReceiverMobile : MonoBehaviour
{
    public int port = 5004;
    private int sampleRate = 48000;

    UdpClient udp;
    ConcurrentQueue<float> audioQueue = new ConcurrentQueue<float>();
    int receivedPackets = 0;

    string taag = "AudioReceiverMobile";

    void Awake()
    {
        var config = AudioSettings.GetConfiguration();
        config.sampleRate = sampleRate;
        AudioSettings.Reset(config);
        Debug.Log(taag + " Sample rate configurado a " + sampleRate);
    }

    void Start()
    {
        Application.runInBackground = true;
        Debug.Log(taag + " AudioReceiver listening on port " + port);


        udp = new UdpClient(port);

        StartReceivingAsync();

        var audioSource = GetComponent<AudioSource>();
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
            Debug.Log(taag + " AudioSource iniciado");
        }
    }

    async void StartReceivingAsync()
    {
        while (true)
        {
            try
            {
                var result = await udp.ReceiveAsync();
                if (result.Buffer.Length == 0) continue;
                receivedPackets++;

                Debug.Log(taag + $" Paquete UDP recibido #{receivedPackets} de {result.RemoteEndPoint}, bytes={result.Buffer.Length}");

                int samples = result.Buffer.Length / 2;
                for (int i = 0; i < samples; i++)
                {
                    short pcm = (short)(result.Buffer[i * 2] | (result.Buffer[i * 2 + 1] << 8));
                    float sample = pcm / 32768f;
                    audioQueue.Enqueue(sample);
                }

                Debug.Log(taag + $" Samples en cola: {audioQueue.Count}");
            }
            catch (ObjectDisposedException)
            {
                Debug.Log(taag + " Socket cerrado, deteniendo recepción");
                break;
            }
            catch (SocketException e)
            {
                Debug.LogError(taag + " Error UDP: " + e.Message);
            }
            catch (System.Exception e)
            {
                Debug.LogError(taag + " Excepción: " + e.Message);
            }
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;

            if (audioQueue.TryDequeue(out sample))
            {
                for (int c = 0; c < channels; c++)
                    data[i + c] = sample;
            }
            else
            {
                for (int c = 0; c < channels; c++)
                    data[i + c] = 0f;
            }
        }
        Debug.Log(taag + $" OnAudioFilterRead llamado, buffer length: {data.Length}, cola restante: {audioQueue.Count}");
    }

    void OnApplicationQuit()
    {
        udp?.Close();
        Debug.Log(taag + " UDP cerrado");
    }
}
