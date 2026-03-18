using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;

[RequireComponent(typeof(Renderer))]
public class WebCamMobile : MonoBehaviour
{
    public int port = 5005;

    UdpClient udpClient;
    Thread receiveThread;

    Texture2D cameraTexture;
    Renderer rend;

    byte[] latestFrame;
    bool newFrame = false;

    void Start()
    {
        rend = GetComponent<Renderer>();

        cameraTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        rend.material.mainTexture = cameraTexture;

        udpClient = new UdpClient(port);

        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        //Debug.Log("Escuchando cámara UDP en puerto " + port);
    }

    void ReceiveLoop()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);

        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);

                latestFrame = data;
                newFrame = true;
            }
            catch { }
        }
    }

    void Update()
    {
        if (newFrame)
        {
            cameraTexture.LoadImage(latestFrame);
            rend.material.mainTexture = cameraTexture;
            newFrame = false;
        }
    }

    void OnApplicationQuit()
    {
        if (receiveThread != null)
            receiveThread.Abort();

        if (udpClient != null)
            udpClient.Close();
    }
}