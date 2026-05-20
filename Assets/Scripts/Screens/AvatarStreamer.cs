using UnityEngine;
using System.Collections;
using System.Net.Sockets;
using System;

// Envía video del avatar (con lip sync) a Android por el puerto 5000.
// Resolución: 640×480, calidad JPEG: 40.
// Android lo recibe en startVideoReceiver() (MainActivity, puerto 5000).
public class AvatarStreamer : MonoBehaviour
{
    [Header("Destino")]
    public string androidIP = "192.168.1.140";
    public int port = 5000;

    [Header("Video")]
    public int width = 320;
    public int height = 240;
    [Range(1, 100)]
    public int quality = 30;

    [Header("Framerate")]
    public float frameInterval = 0.066f;

    public Camera cam;

    private UdpClient udp;
    private RenderTexture rt;
    private Texture2D tex;

    void Start()
    {
        udp = new UdpClient();
        rt = new RenderTexture(width, height, 24) { antiAliasing = 1 };
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        StartCoroutine(StreamLoop());
    }

    IEnumerator StreamLoop()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            SendFrame();
            yield return new WaitForSecondsRealtime(frameInterval);
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

        // UDP tiene límite de ~65507 bytes por paquete.
        // A 640×480 calidad 40, el JPEG suele estar entre 5-15 KB. No hay problema.
        if (jpg.Length < 65507)
        {
            try { udp.Send(jpg, jpg.Length, androidIP, port); }
            catch (Exception e) { Debug.LogWarning("AvatarStreamer send error: " + e.Message); }
        }
        else
        {
            Debug.LogWarning($"AvatarStreamer: frame demasiado grande ({jpg.Length} bytes), descartado. Reduce calidad o resolución.");
        }

        RenderTexture.active = null;
        cam.targetTexture = null;
    }

    void OnDestroy() => udp?.Close();
}
