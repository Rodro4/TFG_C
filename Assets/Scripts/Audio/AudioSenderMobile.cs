using System;
using System.Net.Sockets;
using UnityEngine;
using uLipSync;

// Captura el micrófono en Unity y envía a Android (puerto 5001).
// Sample rate: 16000 Hz, mono, PCM16, chunks de 640 bytes (20ms).
//
// NOTA SOBRE EL MICRÓFONO:
// Unity captura a la frecuencia que el hardware del micrófono soporte.
// Si el micrófono no soporta 16000 Hz nativamente, Unity hará resampleo
// interno, lo que puede introducir un pequeño pitch shift o calidad reducida.
// En PC con micrófonos estándar (44100/48000 Hz) esto es transparente.
// Si detectas pitch incorrecto, ajusta SampleRate a la frecuencia nativa
// de tu micrófono (compruébala con Microphone.GetDeviceCaps).
public class AudioSenderMobile : MonoBehaviour
{
    [Header("Configuración")]
    public string targetIP = "192.168.1.140";
    public int targetPort = 5001;
    public bool isMuted = false;

    // Debe coincidir con FRECUENCIA_AUDIO en MainActivity y AudioReceiverMobile
    private const int SampleRate = 16000;
    // 320 muestras = 20ms @ 16kHz. × 2 bytes = 640 bytes por paquete UDP.
    private const int ChunkSamples = 320;

    private string micName;
    private AudioClip micClip;
    private int lastSamplePosition = 0;
    private UdpClient udp;
    private float[] audioBuffer;
    private byte[] byteBuffer;
    private uLipSyncAudioSource lipSyncProxy;

    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("AudioSenderMobile: no se encontró ningún micrófono.");
            return;
        }

        // Selecciona el primer micrófono disponible.
        // Si quieres elegir uno concreto, cambia el índice o usa el nombre exacto.
        micName = Microphone.devices[0];
        Debug.Log($"AudioSenderMobile: usando micrófono '{micName}'");

        udp = new UdpClient();
        udp.Connect(targetIP, targetPort);

        audioBuffer = new float[ChunkSamples];
        byteBuffer = new byte[ChunkSamples * 2];
        lipSyncProxy = GetComponent<uLipSyncAudioSource>();

        // loop=true, 1 segundo de buffer circular, frecuencia objetivo
        micClip = Microphone.Start(micName, true, 1, SampleRate);
        StartCoroutine(WaitForMic());
    }

    System.Collections.IEnumerator WaitForMic()
    {
        // Espera a que el micrófono haya empezado a grabar de verdad
        while (Microphone.GetPosition(micName) <= 0) yield return null;
        lastSamplePosition = 0;
    }

    void Update()
    {
        if (micClip == null) return;

        int currentPos = Microphone.GetPosition(micName);
        // Muestras disponibles en el buffer circular (con wraparound)
        int available = (currentPos - lastSamplePosition + micClip.samples) % micClip.samples;

        while (available >= ChunkSamples)
        {
            micClip.GetData(audioBuffer, lastSamplePosition);

            // Alimentar lip sync si existe
            lipSyncProxy?.onAudioFilterRead?.Invoke(audioBuffer, 1);

            // Convertir float[-1,1] a PCM16 little-endian
            for (int i = 0; i < ChunkSamples; i++)
            {
                short sample = (short)(Mathf.Clamp(isMuted ? 0f : audioBuffer[i], -1f, 1f) * 32767);
                byteBuffer[i * 2] = (byte)(sample & 0xFF);
                byteBuffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            try { udp.Send(byteBuffer, byteBuffer.Length); }
            catch (Exception e) { Debug.LogWarning("AudioSenderMobile send error: " + e.Message); }

            lastSamplePosition = (lastSamplePosition + ChunkSamples) % micClip.samples;
            available -= ChunkSamples;
        }
    }

    void OnApplicationQuit()
    {
        if (micName != null && Microphone.IsRecording(micName))
            Microphone.End(micName);
        udp?.Close();
    }
}
