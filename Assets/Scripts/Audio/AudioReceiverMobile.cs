using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using UnityEngine;

// Recibe audio de Android (puerto 5004) y lo reproduce.
// Sample rate: 16000 Hz, mono, PCM16, chunks de 640 bytes (20ms).
[RequireComponent(typeof(AudioSource))]
public class AudioReceiverMobile : MonoBehaviour
{
    [Header("Configuración")]
    public int port = 5004;
    public bool isDeafened = false;

    // Debe coincidir con FRECUENCIA_AUDIO en MainActivity y AudioSenderMobile
    private const int SampleRate = 16000;

    // Máximo de muestras en cola antes de descartarlas (evita latencia acumulada).
    // 16000 muestras = 1 segundo de audio a 16kHz.
    private const int MaxQueueSamples = 16000;

    private UdpClient udp;
    private ConcurrentQueue<float> audioQueue = new ConcurrentQueue<float>();

    void Awake()
    {
        // Forzar el sample rate del motor de audio de Unity
        var config = AudioSettings.GetConfiguration();
        config.sampleRate = SampleRate;
        AudioSettings.Reset(config);
    }

    void Start()
    {
        Application.runInBackground = true;
        udp = new UdpClient(port);
        StartReceiving();

        var src = GetComponent<AudioSource>();
        src.loop = true;
        if (!src.isPlaying) src.Play();
    }

    async void StartReceiving()
    {
        while (true)
        {
            try
            {
                var result = await udp.ReceiveAsync();
                if (result.Buffer.Length == 0 || isDeafened) continue;

                // Si la cola se ha llenado demasiado, la vaciamos para recuperar
                // sincronía inmediatamente en lugar de reproducir audio antiguo.
                if (audioQueue.Count > MaxQueueSamples)
                {
                    float discard;
                    while (audioQueue.TryDequeue(out discard)) { }
                }

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

    // Unity llama a este método en el hilo de audio a la frecuencia configurada.
    // Silencios explícitos cuando no hay datos -> sin pops ni latencia acumulada.
    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;
            audioQueue.TryDequeue(out sample);
            for (int c = 0; c < channels; c++)
                data[i + c] = sample;
        }
    }

    void OnApplicationQuit() => udp?.Close();
}
