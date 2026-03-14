using UnityEngine;
using System.Net.Sockets;

public class AvatarStreamer : MonoBehaviour
{
    public Camera cam;
    public string androidIP = "192.168.1.140";

    UdpClient udp;

    RenderTexture rt;
    Texture2D tex;

    void Start()
    {
        udp = new UdpClient();

        rt = new RenderTexture(640, 480, 24);
        tex = new Texture2D(640, 480, TextureFormat.RGB24, false);

        InvokeRepeating(nameof(SendFrame), 0f, 0.05f);
    }

    void SendFrame()
    {
        cam.targetTexture = rt;

        cam.Render();

        RenderTexture.active = rt;

        tex.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
        tex.Apply();

        byte[] jpg = tex.EncodeToJPG(50);

        udp.Send(jpg, jpg.Length, androidIP, 5000);

        RenderTexture.active = null;
        cam.targetTexture = null;
    }
}