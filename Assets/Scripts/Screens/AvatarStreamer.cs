using UnityEngine;
using System.Collections;
using System.Net.Sockets;
using System;

public class AvatarStreamer : MonoBehaviour
{
    [Header("Configuración")]
    public string androidIP = "192.168.1.13";
    public int port = 5000;
    public int width = 720;
    public int height = 480;
    [Range(1, 100)] public int quality = 70;
    public float frameRate = 0.05f;

    public Camera cam;
    private UdpClient udp;
    private RenderTexture rt;
    private Texture2D tex;

    void Start()
    {
        udp = new UdpClient();
        rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        StartCoroutine(StreamLoop());
    }

    IEnumerator StreamLoop()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            SendFrame();
            yield return new WaitForSecondsRealtime(frameRate);
        }
    }

    void SendFrame()
    {
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;

        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        byte[] jpg = tex.EncodeToJPG(quality);

        if (jpg.Length < 65507)
        {
            try { udp.Send(jpg, jpg.Length, androidIP, port); } catch { }
        }

        RenderTexture.active = null;
        cam.targetTexture = null;
    }

    void OnDestroy() => udp?.Close();
}