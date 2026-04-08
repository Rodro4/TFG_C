using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;

[RequireComponent(typeof(Renderer))]
public class RGBDImageReceiverUDP : MonoBehaviour
{
    public int rgbPort = 5006;
    public int depthPort = 5007;

    private UdpClient rgbClient;
    private UdpClient depthClient;

    private Thread rgbThread;
    private Thread depthThread;

    private Texture2D rgbTexture;
    private Texture2D depthTexture;

    private Renderer rend;

    private byte[] latestRGB;
    private byte[] latestDepth;

    private readonly object lockRGB = new object();
    private readonly object lockDepth = new object();

    private bool newRGB = false;
    private bool newDepth = false;

    public bool showRGB = true;

    void Start()
    {
        rend = GetComponent<Renderer>();

        rgbTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        depthTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);

        rend.material.mainTexture = rgbTexture;

        // UDP RGB
        rgbClient = new UdpClient(rgbPort);
        rgbClient.Client.ReceiveBufferSize = 1024 * 1024;

        // UDP DEPTH
        depthClient = new UdpClient(depthPort);
        depthClient.Client.ReceiveBufferSize = 1024 * 1024;

        // Threads
        rgbThread = new Thread(ReceiveRGB);
        rgbThread.IsBackground = true;
        rgbThread.Start();

        depthThread = new Thread(ReceiveDepth);
        depthThread.IsBackground = true;
        depthThread.Start();

        Debug.Log("RGB en puerto " + rgbPort);
        Debug.Log("DEPTH en puerto " + depthPort);
    }

    void ReceiveRGB()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, rgbPort);

        while (true)
        {
            try
            {
                byte[] data = rgbClient.Receive(ref anyIP);

                lock (lockRGB)
                {
                    latestRGB = data;
                    newRGB = true;
                }
            }
            catch { }
        }
    }

    void ReceiveDepth()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, depthPort);

        while (true)
        {
            try
            {
                byte[] data = depthClient.Receive(ref anyIP);

                lock (lockDepth)
                {
                    latestDepth = data;
                    newDepth = true;
                }
            }
            catch { }
        }
    }

    void Update()
    {
        if (showRGB)
        {
            if (newRGB)
            {
                byte[] frameCopy;

                lock (lockRGB)
                {
                    frameCopy = latestRGB;
                    newRGB = false;
                }

                if (frameCopy != null && frameCopy.Length > 100)
                {
                    bool loaded = rgbTexture.LoadImage(frameCopy);

                    if (loaded)
                    {
                        rgbTexture.Apply();
                        rend.material.mainTexture = rgbTexture;
                    }
                    else
                    {
                        Debug.LogWarning("Frame RGB corrupto");
                    }
                }
            }
        }
        else
        {
            if (newDepth)
            {
                byte[] frameCopy;

                lock (lockDepth)
                {
                    frameCopy = latestDepth;
                    newDepth = false;
                }

                if (frameCopy != null && frameCopy.Length > 100)
                {
                    bool loaded = depthTexture.LoadImage(frameCopy);

                    if (loaded)
                    {
                        depthTexture.Apply();
                        rend.material.mainTexture = depthTexture;
                    }
                    else
                    {
                        Debug.LogWarning("Frame DEPTH corrupto");
                    }
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        if (rgbThread != null) rgbThread.Abort();
        if (depthThread != null) depthThread.Abort();

        if (rgbClient != null) rgbClient.Close();
        if (depthClient != null) depthClient.Close();
    }

    public void ShowRGB()
    {
        showRGB = true;
    }

    public void ShowDepth()
    {
        showRGB = false;
    }

    public Texture2D GetRGBTexture()
    {
        return rgbTexture;
    }

    public Texture2D GetDepthTexture()
    {
        return depthTexture;
    }
}