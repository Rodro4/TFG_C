////using UnityEngine;
////using RosSharp.RosBridgeClient;
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.AudioCommon;
//using System;
//using System.Collections;

///// <summary>
///// Captura audio del micrófono de Unity y publica bloques de audio en ROS
///// como mensajes audio_common_msgs/AudioData.
///// Incluye control de buffer circular, retardo inicial y conversión estéreo->mono.
///// </summary>
//public class MicrophonePublisher : MonoBehaviour
//{
//    [Header("ROS")]
//    public string topic = "/audio_out";

//    [Header("Audio Settings")]
//    public string deviceName = null;     // null = dispositivo por defecto
//    public int sampleRate = 16000;       // compatible con audio_common
//    public int bufferSeconds = 1;        // tamaño del buffer circular
//    public float readDelaySec = 0.1f;    // retardo para evitar zona sin grabar

//    private RosSocket rosSocket;
//    private string publicationId;
//    private AudioClip micClip;
//    private int lastSamplePos;
//    private int bufferSize;
//    private bool initialized = false;

//    void Start()
//    {
//        StartCoroutine(InitAndConnect());
//    }

//    private IEnumerator InitAndConnect()
//    {
//        var connector = FindObjectOfType<RosConnector>();
//        if (connector == null)
//        {
//            Debug.LogError("MicrophonePublisher: RosConnector no encontrado.");
//            yield break;
//        }

//        // Esperar conexión ROS
//        while (connector.RosSocket == null)
//        {
//            Debug.Log("MicrophonePublisher: Esperando conexión con rosbridge...");
//            yield return new WaitForSeconds(0.5f);
//        }

//        rosSocket = connector.RosSocket;
//        publicationId = rosSocket.Advertise<AudioData>(topic);
//        Debug.Log("MicrophonePublisher: Conectado. Publicando audio en " + topic);

//        // Esperar micrófono
//        if (Microphone.devices.Length == 0)
//        {
//            Debug.LogError("MicrophonePublisher: No se detectó ningún micrófono.");
//            yield break;
//        }

//        if (deviceName == null)
//            deviceName = Microphone.devices[0];

//        micClip = Microphone.Start(deviceName, true, bufferSeconds, sampleRate);
//        Debug.Log($"MicrophonePublisher: Grabando desde '{deviceName}' a {sampleRate}Hz.");

//        yield return new WaitUntil(() => Microphone.GetPosition(deviceName) > 0);

//        bufferSize = micClip.samples;
//        lastSamplePos = 0;
//        initialized = true;

//        Debug.Log($"MicrophonePublisher: Buffer inicializado con {bufferSize} muestras.");

//        // Bucle continuo de lectura y envío
//        while (true)
//        {
//            yield return new WaitForSeconds(readDelaySec);
//            PublishAudioChunk();
//        }
//    }

//    private void PublishAudioChunk()
//    {
//        if (!initialized || !Microphone.IsRecording(deviceName) || rosSocket == null)
//            return;

//        int micPos = Microphone.GetPosition(deviceName);
//        int sampleCount = micPos - lastSamplePos;
//        if (sampleCount < 0) sampleCount += bufferSize;
//        if (sampleCount < 1) return;

//        float[] data = new float[sampleCount * micClip.channels];
//        int readStart = (lastSamplePos + bufferSize - sampleCount) % bufferSize;
//        micClip.GetData(data, readStart);
//        lastSamplePos = micPos;

//        // Convertir a mono si es necesario
//        int frames = data.Length / micClip.channels;
//        float[] mono = new float[frames];
//        for (int i = 0; i < frames; i++)
//        {
//            float sum = 0f;
//            for (int c = 0; c < micClip.channels; c++)
//                sum += data[i * micClip.channels + c];
//            mono[i] = sum / micClip.channels;
//        }

//        // Convertir a bytes (16 bit PCM)
//        byte[] byteBuffer = new byte[mono.Length * 2];
//        int j = 0;
//        foreach (float sample in mono)
//        {
//            short s = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
//            BitConverter.GetBytes(s).CopyTo(byteBuffer, j);
//            j += 2;
//        }

//        if (byteBuffer.Length > 0)
//        {
//            rosSocket.Publish(publicationId, new AudioData(byteBuffer));
//            Debug.Log($"MicrophonePublisher: Enviado bloque con {mono.Length} muestras.");
//        }
//    }
//}











using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.AudioCommon;
using System;
using System.Collections;

public class MicrophonePublisher : MonoBehaviour
{
    public string topic = "/audio_out";
    public string deviceName = null;
    public int sampleRate = 44100;   // más compatible con Yeti Nano
    public int bufferSeconds = 1;
    public float readDelaySec = 0.1f;

    private RosSocket rosSocket;
    private string publicationId;
    private AudioClip micClip;
    private int lastSamplePos;
    private int bufferSize;
    private bool initialized = false;

    IEnumerator Start()
    {
        // Esperar conexión ROS
        var connector = FindObjectOfType<RosConnector>();
        while (connector == null || connector.RosSocket == null)
        {
            Debug.Log("MicrophonePublisher Esperando rosbridge...");
            yield return new WaitForSeconds(0.5f);
            connector = FindObjectOfType<RosConnector>();
        }

        rosSocket = connector.RosSocket;
        publicationId = rosSocket.Advertise<AudioData>(topic);
        Debug.Log("MicrophonePublisher Conectado al rosbridge y publicando en " + topic);

        // Detectar micrófono
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("MicrophonePublisher No hay micrófonos disponibles.");
            yield break;
        }

        if (string.IsNullOrEmpty(deviceName))
            deviceName = Microphone.devices[0];

        micClip = Microphone.Start(deviceName, true, bufferSeconds, sampleRate);
        Debug.Log($"MicrophonePublisher Grabando desde '{deviceName}' a {sampleRate}Hz.");

        // Esperar a que haya muestras reales
        yield return new WaitUntil(() => Microphone.GetPosition(deviceName) > 0);
        yield return new WaitForSeconds(0.5f); // retardo adicional

        bufferSize = micClip.samples;
        initialized = true;
        lastSamplePos = 0;

        Debug.Log($"MicrophonePublisher Buffer inicializado con {bufferSize} muestras.");

        // Bucle continuo de publicación
        while (true)
        {
            yield return new WaitForSeconds(readDelaySec);
            PublishChunk();
        }
    }

    private void PublishChunk()
    {
        if (!initialized || !Microphone.IsRecording(deviceName) || rosSocket == null)
            return;

        int micPos = Microphone.GetPosition(deviceName);
        int sampleCount = micPos - lastSamplePos;
        if (sampleCount < 0) sampleCount += bufferSize;
        if (sampleCount == 0) return;

        float[] raw = new float[sampleCount * micClip.channels];
        micClip.GetData(raw, lastSamplePos);
        lastSamplePos = micPos;

        // Medir amplitud media para depurar
        float avg = 0f;
        foreach (float s in raw)
            avg += Mathf.Abs(s);
        avg /= raw.Length;

        Debug.Log($"MicrophonePublisher Capturado bloque {sampleCount}x{micClip.channels} | Media amplitud: {avg:F5}");

        // Si la amplitud es casi cero, el audio no llega
        if (avg < 0.0001f)
        {
            Debug.LogWarning("MicrophonePublisher Audio nulo: el micrófono no devuelve señal.");
            return;
        }

        // Convertir a mono si hay varios canales
        int frames = raw.Length / micClip.channels;
        float[] mono = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            float sum = 0;
            for (int c = 0; c < micClip.channels; c++)
                sum += raw[i * micClip.channels + c];
            mono[i] = sum / micClip.channels;
        }

        // Convertir a bytes PCM16
        byte[] buffer = new byte[mono.Length * 2];
        int j = 0;
        foreach (float sample in mono)
        {
            short s = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
            BitConverter.GetBytes(s).CopyTo(buffer, j);
            j += 2;
        }

        rosSocket.Publish(publicationId, new AudioData(buffer));
    }
}
