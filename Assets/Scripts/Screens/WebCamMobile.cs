using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;

// Recibe el stream de la cámara de Android (puerto 5005) y lo muestra en un Renderer.
// Android lo envía desde startCameraSender() a 640×480, calidad JPEG 40.
[RequireComponent(typeof(Renderer))]
public class WebCamMobile : MonoBehaviour
{
    [Header("Configuración")]
    public int port = 5005;

    private UdpClient udpClient;
    private Thread receiveThread;
    private Texture2D cameraTexture;
    private Renderer rend;

    // Usamos dos buffers para intercambiar en el hilo principal sin bloquear el receptor.
    // El hilo de red escribe en 'pendingFrame'; el Update lo consume.
    private byte[] pendingFrame = null;
    private readonly object frameLock = new object();
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
                byte[] data = udpClient.Receive(ref anyIP);
                // Siempre sobreescribimos con el frame MÁS RECIENTE.
                // Si Update aún no procesó el anterior, lo descartamos ? sin latencia acumulada.
                lock (frameLock)
                {
                    pendingFrame = data;
                    newFrame = true;
                }
            }
            catch { break; }
        }
    }

    void Update()
    {
        byte[] frame = null;
        lock (frameLock)
        {
            if (newFrame)
            {
                frame = pendingFrame;
                newFrame = false;
            }
        }

        if (frame != null)
        {
            cameraTexture.LoadImage(frame); // Decodifica JPEG y redimensiona automáticamente
            cameraTexture.Apply();
        }
    }

    void OnApplicationQuit()
    {
        udpClient?.Close();
        receiveThread?.Abort();
    }
}
