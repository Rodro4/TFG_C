using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;

[RequireComponent(typeof(Renderer))]
public class WebCamMobile : MonoBehaviour
{
    [Header("Configuración")]
    public int port = 5005;

    private UdpClient udpClient;
    private Thread receiveThread;
    private Texture2D cameraTexture;
    private Renderer rend;
    private byte[] latestFrame;
    private bool newFrame = false;

    void Start()
    {
        rend = GetComponent<Renderer>();
        cameraTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        rend.material.mainTexture = cameraTexture;

        udpClient = new UdpClient(port);
        receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        receiveThread.Start();
    }

    void ReceiveLoop()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);
        while (true)
        {
            try
            {
                latestFrame = udpClient.Receive(ref anyIP);
                newFrame = true;
            }
            catch { break; }
        }
    }

    void Update()
    {
        if (newFrame && latestFrame != null)
        {
            cameraTexture.LoadImage(latestFrame);
            cameraTexture.Apply();
            newFrame = false;
        }
    }

    void OnApplicationQuit()
    {
        udpClient?.Close();
        receiveThread?.Abort();
    }
}