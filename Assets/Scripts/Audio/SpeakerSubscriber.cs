//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.AudioCommon;
//using System;
//using System.Collections;

//public class SpeakerSubscriber : MonoBehaviour
//{
//    public string topic = "/audio_in";
//    private RosSocket rosSocket;
//    private AudioSource audioSource;
//    private const int sampleRate = 16000;

//    void Start()
//    {
//        audioSource = gameObject.AddComponent<AudioSource>();
//        audioSource.loop = false;
//        audioSource.playOnAwake = false;

//        StartCoroutine(WaitForConnection());
//    }

//    private IEnumerator WaitForConnection()
//    {
//        var connector = FindObjectOfType<RosConnector>();
//        if (connector == null)
//        {
//            Debug.LogError("SpeakerSubscriber: RosConnector no encontrado en la escena.");
//            yield break;
//        }

//        while (connector.RosSocket == null)
//        {
//            Debug.Log("SpeakerSubscriber: Esperando a conexión con rosbridge...");
//            yield return new WaitForSeconds(0.5f);
//        }

//        rosSocket = connector.RosSocket;
//        rosSocket.Subscribe<AudioData>(topic, ReceiveAudio);
//        Debug.Log("SpeakerSubscriber: Conectado y suscrito a " + topic);
//    }

//    private void ReceiveAudio(AudioData message)
//    {
//        int sampleCount = message.data.Length / 2;
//        if (sampleCount == 0) return;

//        float[] samples = new float[sampleCount];
//        for (int i = 0; i < sampleCount; i++)
//        {
//            short s = BitConverter.ToInt16(message.data, i * 2);
//            samples[i] = s / (float)short.MaxValue;
//        }

//        var clip = AudioClip.Create("ROS_Audio", sampleCount, 1, sampleRate, false);
//        clip.SetData(samples, 0);

//        // Ejecutar en el hilo principal
//        UnityMainThreadDispatcher.Instance().Enqueue(() =>
//        {
//            audioSource.clip = clip;
//            audioSource.Play();
//        });
//    }
//}














using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.AudioCommon;
using System;
using System.Collections;

/// <summary>
/// Recibe mensajes audio_common_msgs/AudioData desde ROS y los reproduce en Unity.
/// Incluye normalización de volumen y seguridad de hilo principal.
/// </summary>
public class SpeakerSubscriber : MonoBehaviour
{
    [Header("ROS")]
    public string topic = "/audio_in";

    [Header("Audio Settings")]
    public int sampleRate = 16000;
    [Range(0f, 2f)] public float volume = 1.0f;

    private RosSocket rosSocket;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;

        StartCoroutine(WaitForConnection());
    }

    private IEnumerator WaitForConnection()
    {
        var connector = FindObjectOfType<RosConnector>();
        if (connector == null)
        {
            Debug.LogError("SpeakerSubscriber: RosConnector no encontrado.");
            yield break;
        }

        while (connector.RosSocket == null)
        {
            Debug.Log("SpeakerSubscriber: Esperando conexión con rosbridge...");
            yield return new WaitForSeconds(0.5f);
        }

        rosSocket = connector.RosSocket;
        rosSocket.Subscribe<AudioData>(topic, ReceiveAudio);
        Debug.Log("SpeakerSubscriber: Conectado y suscrito a " + topic);
    }

    private void ReceiveAudio(AudioData message)
    {
        if (message.data == null || message.data.Length == 0)
            return;

        int sampleCount = message.data.Length / 2;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short s = BitConverter.ToInt16(message.data, i * 2);
            samples[i] = s / (float)short.MaxValue;
        }

        // Normalizar ligeramente para evitar saturación
        float maxVal = 0f;
        foreach (float f in samples)
            if (Mathf.Abs(f) > maxVal) maxVal = Mathf.Abs(f);
        if (maxVal > 0.9f)
        {
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= 0.9f / maxVal;
        }

        AudioClip clip = AudioClip.Create("ROS_Audio", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.Play();
        });
    }
}
