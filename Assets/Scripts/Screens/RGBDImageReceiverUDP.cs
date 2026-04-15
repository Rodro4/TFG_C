using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class RGBDImageReceiverUDP : MonoBehaviour
{
    [Header("Configuración")]
    public int rgbPort = 5006;
    public int depthPort = 5007;
    public bool mostrarRGB = true;

    private UdpClient rgbClient, depthClient;
    private Thread rgbThread, depthThread;
    private Texture2D rgbTex, depthTex;
    private Renderer rend;
    private byte[] dataRGB, dataDepth;
    private bool nuevoRGB, nuevoDepth;
    private readonly object lockObj = new object();

    void Start()
    {
        rend = GetComponent<Renderer>();
        rgbTex = new Texture2D(2, 2);
        depthTex = new Texture2D(2, 2);

        rgbClient = new UdpClient(rgbPort);
        depthClient = new UdpClient(depthPort);

        (rgbThread = new Thread(() => Escuchar(rgbClient, true))).Start();
        (depthThread = new Thread(() => Escuchar(depthClient, false))).Start();
    }

    void Escuchar(UdpClient client, bool esRGB)
    {
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            try
            {
                byte[] data = client.Receive(ref ep);
                lock (lockObj)
                {
                    if (esRGB) { dataRGB = data; nuevoRGB = true; }
                    else { dataDepth = data; nuevoDepth = true; }
                }
            }
            catch { }
        }
    }

    void Update()
    {
        byte[] frame = null;
        bool hayActualizacion = false;

        lock (lockObj)
        {
            if (mostrarRGB && nuevoRGB) { frame = dataRGB; nuevoRGB = false; hayActualizacion = true; }
            else if (!mostrarRGB && nuevoDepth) { frame = dataDepth; nuevoDepth = false; hayActualizacion = true; }
        }

        if (hayActualizacion && frame != null)
        {
            Texture2D tex = mostrarRGB ? rgbTex : depthTex;
            if (tex.LoadImage(frame)) rend.material.mainTexture = tex;
        }
    }

    void OnApplicationQuit()
    {
        rgbThread?.Abort(); depthThread?.Abort();
        rgbClient?.Close(); depthClient?.Close();
    }

    public void ShowRGB()
    {
        mostrarRGB = true;
    }

    public void ShowDepth()
    {
        mostrarRGB = false;
    }
}