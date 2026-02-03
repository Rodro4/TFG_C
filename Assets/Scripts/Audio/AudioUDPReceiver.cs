using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioUDPReceiver : MonoBehaviour
{
    public int port = 61000;
    public int sampleRate = 44100;
    public int channels = 1;
    public float bufferSeconds = 1f; // 1 segundo de buffer

    private Process gstProcess;
    private Thread readThread;
    private AudioSource audioSource;
    private AudioClip audioClip;

    private float[] audioBuffer;
    private int bufferSize;
    private int writePos = 0;
    private int readPos = 0;
    private object bufferLock = new object();

    void Start()
    {
        bufferSize = Mathf.CeilToInt(sampleRate * channels * bufferSeconds);
        audioBuffer = new float[bufferSize];

        audioSource = GetComponent<AudioSource>();
        audioClip = AudioClip.Create("RemoteAudio", bufferSize, channels, sampleRate, true, OnAudioRead);
        audioSource.clip = audioClip;
        audioSource.loop = true;

        StartGStreamer();

        // Esperamos un pequeño tiempo para que el buffer se llene
        Invoke(nameof(StartAudio), 0.1f);
    }

    void StartAudio()
    {
        audioSource.Play();
    }

    void StartGStreamer()
    {
        gstProcess = new Process();
        gstProcess.StartInfo.FileName = @"C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe";
        gstProcess.StartInfo.Arguments =
            $"udpsrc port={port} caps=\"audio/x-raw,format=S16LE,rate={sampleRate},channels={channels}\" " +
            "! audioconvert ! audioresample ! fdsink fd=1 sync=false";

        gstProcess.StartInfo.UseShellExecute = false;
        gstProcess.StartInfo.RedirectStandardOutput = true;
        gstProcess.StartInfo.RedirectStandardError = true;
        gstProcess.StartInfo.CreateNoWindow = true;
        gstProcess.Start();

        readThread = new Thread(ReadAudio);
        readThread.IsBackground = true;
        readThread.Start();
    }

    void ReadAudio()
    {
        BinaryReader reader = new BinaryReader(gstProcess.StandardOutput.BaseStream);
        int sampleBytes = 2 * channels;
        byte[] buffer = new byte[1024 * sampleBytes];

        while (true)
        {
            int bytesRead = reader.Read(buffer, 0, buffer.Length);
            if (bytesRead <= 0) continue;

            int samplesRead = bytesRead / 2;
            lock (bufferLock)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    short s = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
                    audioBuffer[writePos] = s / 32768f;

                    // Avanzar writePos
                    writePos = (writePos + 1) % bufferSize;

                    // Si writePos alcanza readPos, mover readPos adelante (descartar antiguo)
                    if (writePos == readPos)
                    {
                        readPos = (readPos + 1) % bufferSize;
                    }
                }
            }
        }
    }

    void OnAudioRead(float[] data)
    {
        lock (bufferLock)
        {
            int available = (writePos - readPos + bufferSize) % bufferSize;

            int maxLatencySamples = Mathf.Min(bufferSize / 2, (int)(sampleRate * channels * 0.3f)); // ~300ms
            if (available > maxLatencySamples)
            {
                readPos = (writePos - maxLatencySamples + bufferSize) % bufferSize;
                available = maxLatencySamples;
            }

            for (int i = 0; i < data.Length; i++)
            {
                if (i < available)
                {
                    data[i] = audioBuffer[readPos];
                    readPos = (readPos + 1) % bufferSize;
                }
                else
                {
                    data[i] = 0f; // silencio
                }
            }
        }
    }

    void OnDestroy()
    {
        if (gstProcess != null && !gstProcess.HasExited)
            gstProcess.Kill();

        if (readThread != null && readThread.IsAlive)
            readThread.Abort();
    }
}
