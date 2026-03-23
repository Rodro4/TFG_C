using UnityEngine;
using System.Collections;
using System.Net.Sockets;

public class AvatarStreamer : MonoBehaviour
{
    public Camera cam;
    public string androidIP = "192.168.1.140";
    public int port = 5000;

    [Header("Calidad")]
    public int width = 640;
    public int height = 480;
    [Range(1, 100)]
    public int quality = 75;

    private UdpClient udp;
    private RenderTexture rt;
    private Texture2D tex;

    void Start()
    {
        udp = new UdpClient();
        rt = new RenderTexture(width, height, 24);
        rt.antiAliasing = 4;

        tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        StartCoroutine(StreamLoop());
    }

    IEnumerator StreamLoop()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            SendFrame();
            yield return new WaitForSecondsRealtime(0.05f);
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
            try
            {
                udp.Send(jpg, jpg.Length, androidIP, port);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error UDP: " + e.Message);
            }
        }

        RenderTexture.active = null;
        cam.targetTexture = null;
    }

    void OnDestroy() => udp?.Close();
}