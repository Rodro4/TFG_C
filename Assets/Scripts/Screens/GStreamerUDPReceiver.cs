using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

public class GStreamerUDPReceiver : MonoBehaviour
{
    public Renderer targetRenderer;

    private Texture2D tex;
    private Process gstProcess;
    private Thread readThread;
    private byte[] latestFrame;
    private object frameLock = new object();

    void Start()
    {
        tex = new Texture2D(640, 480, TextureFormat.RGB24, false);
        targetRenderer.material.mainTexture = tex;
        targetRenderer.material.mainTextureOffset = new Vector2(0.14f, 0f);

        gstProcess = new Process();

        gstProcess.StartInfo.FileName =
            @"C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe";

        gstProcess.StartInfo.Arguments =
        "udpsrc port=5600 " +
        "! application/x-rtp,media=video,encoding-name=H264,payload=96 " +
        "! rtph264depay " +
        "! avdec_h264 " +
        "! videoconvert " +
        "! videoflip method=horizontal-flip " +
        "! video/x-raw,format=RGB,width=640,height=480 " +
        "! fdsink fd=1 sync=false";


        gstProcess.StartInfo.UseShellExecute = false;
        gstProcess.StartInfo.RedirectStandardOutput = true;
        gstProcess.StartInfo.RedirectStandardError = true;
        gstProcess.StartInfo.CreateNoWindow = true;

        gstProcess.Start();

        readThread = new Thread(ReadFrames);
        readThread.IsBackground = true;
        readThread.Start();
    }

    void ReadFrames()
    {
        int frameSize = 640 * 480 * 3;
        BinaryReader reader = new BinaryReader(gstProcess.StandardOutput.BaseStream);

        while (true)
        {
            byte[] data = reader.ReadBytes(frameSize);
            if (data.Length < frameSize) continue;

            lock (frameLock)
            {
                latestFrame = data;
            }
        }
    }

    void Update()
    {
        if (latestFrame != null)
        {
            lock (frameLock)
            {
                tex.LoadRawTextureData(latestFrame);
                tex.Apply();
                latestFrame = null;
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
