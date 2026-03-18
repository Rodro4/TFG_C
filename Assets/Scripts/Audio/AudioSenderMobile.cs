using System;
using System.Net.Sockets;
using UnityEngine;
using uLipSync;

public class AudioSenderMobile : MonoBehaviour
{
    public string targetIP = "192.168.1.140";
    public int targetPort = 5001;

    private string micName;
    private AudioClip micClip;

    private int sampleRate = 48000;

    private int chunkSize = 480;

    private int lastSamplePosition = 0;

    private UdpClient udp;

    private float[] audioBuffer;
    private byte[] byteBuffer;

    private uLipSyncAudioSource lipSyncProxy;

    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            //Debug.LogError("No hay micrófono detectado");
            return;
        }

        micName = Microphone.devices[0];
        //Debug.Log("Usando micrófono: " + micName);

        udp = new UdpClient();
        udp.Connect(targetIP, targetPort);

        audioBuffer = new float[chunkSize];
        byteBuffer = new byte[chunkSize * 2];
        lipSyncProxy = GetComponent<uLipSyncAudioSource>();

        micClip = Microphone.Start(micName, true, 1, sampleRate);

        StartCoroutine(WaitForMic());
    }

    System.Collections.IEnumerator WaitForMic()
    {
        //Debug.Log("Esperando micrófono...");

        while (Microphone.GetPosition(micName) <= 0)
            yield return null;

        lastSamplePosition = 0;

        //Debug.Log("Micrófono listo para enviar audio");
    }

    void Update()
    {
        if (micClip == null)
            return;

        int currentPosition = Microphone.GetPosition(micName);

        int samplesAvailable = currentPosition - lastSamplePosition;

        if (samplesAvailable < 0)
            samplesAvailable += micClip.samples;

        while (samplesAvailable >= chunkSize)
        {
            micClip.GetData(audioBuffer, lastSamplePosition);

            if (lipSyncProxy != null && lipSyncProxy.onAudioFilterRead != null)
            {
                lipSyncProxy.onAudioFilterRead.Invoke(audioBuffer, 1);
            }

            for (int i = 0; i < chunkSize; i++)
            {
                short sample = (short)(Mathf.Clamp(audioBuffer[i], -1f, 1f) * short.MaxValue);

                byteBuffer[i * 2] = (byte)(sample & 0xFF);
                byteBuffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            try
            {
                udp.Send(byteBuffer, byteBuffer.Length);
            }
            catch (Exception e)
            {
                Debug.LogError("Error enviando UDP: " + e.Message);
            }

            lastSamplePosition += chunkSize;
            lastSamplePosition %= micClip.samples;

            samplesAvailable -= chunkSize;
        }
    }

    void OnApplicationQuit()
    {
        if (Microphone.IsRecording(micName))
            Microphone.End(micName);

        udp?.Close();
    }
}