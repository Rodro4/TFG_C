//using Unity.WebRTC;
//using UnityEngine;
//using System;
//using System.Net.Sockets;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Collections;
//using UnityEngine.UI;

//public class WebRTCVideoReceiver : MonoBehaviour
//{
//    [Header("Gateway Settings")]
//    public string gatewayIP = "172.26.180.93";
//    public int signalingPort = 9999;

//    [Header("Output")]
//    public Renderer targetRenderer;
//    public RawImage targetRawImage;

//    private RTCPeerConnection pc;
//    private CancellationTokenSource cts;
//    private MediaStream receiveStream;

//    void Start()
//    {
//        cts = new CancellationTokenSource();

//        // --- CREAR PEER CONNECTION ---
//        pc = new RTCPeerConnection();
//        pc.AddTransceiver(TrackKind.Video);

//        // --- CREAR MEDIASTREAM RECEPTOR ---
//        receiveStream = new MediaStream();

//        receiveStream.OnAddTrack = e =>
//        {
//            Debug.Log("WebRTCVideoReceiver: OnAddTrack -> ha llegado una pista remota");

//            if (e.Track is VideoStreamTrack videoTrack)
//            {
//                Debug.Log("WebRTCVideoReceiver: pista de vídeo remota detectada");

//                // Inicia el loop que copia track.Texture al plano/RawImage
//                StartCoroutine(RenderVideoCoroutine(videoTrack));
//            }
//        };

//        // --- CUANDO LLEGA UN TRACK LO METEMOS EN receiveStream ---
//        pc.OnTrack = (RTCTrackEvent e) =>
//        {
//            Debug.Log($"WebRTCVideoReceiver: OnTrack recibido ({e.Track.Kind})");

//            if (e.Track.Kind == TrackKind.Video)
//            {
//                Debug.Log("WebRTCVideoReceiver: Añadiendo pista de vídeo al MediaStream receptor");
//                receiveStream.AddTrack(e.Track);
//            }
//        };

//        StartCoroutine(CreateAndSendOffer());
//    }

//    IEnumerator CreateAndSendOffer()
//    {
//        var op = pc.CreateOffer();
//        yield return op;

//        var offer = op.Desc;

//        var setLocal = pc.SetLocalDescription(ref offer);
//        yield return setLocal;

//        _ = SendOfferToGatewayAsync(offer.sdp, cts.Token);
//    }

//    async Task SendOfferToGatewayAsync(string sdp, CancellationToken token)
//    {
//        try
//        {
//            using var client = new TcpClient();
//            await client.ConnectAsync(gatewayIP, signalingPort);
//            Debug.Log("WebRTCVideoReceiver: Conectado al Gateway");

//            NetworkStream stream = client.GetStream();

//            byte[] offerData = Encoding.UTF8.GetBytes(sdp);
//            await stream.WriteAsync(offerData, 0, offerData.Length, token);

//            // Recibir ANSWER
//            byte[] buffer = new byte[65536];
//            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);

//            string answerSdp = Encoding.UTF8.GetString(buffer, 0, bytesRead);

//            Debug.Log($"WebRTCVideoReceiver: ANSWER recibida ({bytesRead} bytes):\n{answerSdp}");

//            var answer = new RTCSessionDescription
//            {
//                type = RTCSdpType.Answer,
//                sdp = answerSdp
//            };

//            StartCoroutine(SetRemoteDescriptionCoroutine(answer));
//        }
//        catch (Exception ex)
//        {
//            Debug.LogError($"WebRTCVideoReceiver: Error de señalización: {ex.Message}");
//        }
//    }

//    IEnumerator SetRemoteDescriptionCoroutine(RTCSessionDescription answer)
//    {
//        var op = pc.SetRemoteDescription(ref answer);
//        yield return op;

//        Debug.Log("WebRTCVideoReceiver: Conexión WebRTC completada (vídeo establecido)");
//    }

//    // --- BUCLE PRINCIPAL PARA ACTUALIZAR LA TEXTURA ---
//    IEnumerator RenderVideoCoroutine(VideoStreamTrack track)
//    {
//        Debug.Log("WebRTCVideoReceiver: esperando frames...");
//        float dbgT = 0f;

//        while (true)
//        {
//            dbgT += Time.deltaTime;
//            if (dbgT > 0.5f)
//            {
//                dbgT = 0f;
//                Debug.Log($"WebRTCVideoReceiver: FrameCheck -> tex = {(track.Texture != null ? $"{track.Texture.width}x{track.Texture.height}" : "NULL")}");
//            }

//            var tex = track.Texture;

//            if (tex != null)
//            {
//                // Usar Renderer
//                if (targetRenderer != null)
//                    targetRenderer.material.mainTexture = tex;

//                // Usar RawImage
//                if (targetRawImage != null)
//                    targetRawImage.texture = tex;
//            }

//            yield return null;
//        }
//    }

//    void Update()
//    {
//        WebRTC.Update();
//    }

//    void OnDestroy()
//    {
//        cts?.Cancel();
//        pc?.Close();
//    }
//}






































using UnityEngine;
using Unity.WebRTC;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

public class MinimalRedVideoReceiver : MonoBehaviour
{
    public string gatewayIP = "127.0.0.1";  // IP del servidor Python
    public int gatewayPort = 9999;
    public Renderer targetRenderer;

    private RTCPeerConnection pc;
    private RenderTexture renderTexture;

    void Start()
    {
        WebRTC.Initialize();
        pc = new RTCPeerConnection();
        pc.AddTransceiver(TrackKind.Video);

        pc.OnTrack = e =>
        {
            if (e.Track is VideoStreamTrack videoTrack)
            {
                Debug.Log("Pista de vídeo recibida");

                // Crear RenderTexture
                renderTexture = new RenderTexture(640, 480, 0, RenderTextureFormat.ARGB32);
                renderTexture.Create();

                if (targetRenderer != null)
                    targetRenderer.material.mainTexture = renderTexture;

                // Copiar cada frame a RenderTexture usando Graphics.Blit
                videoTrack.OnVideoReceived += tex =>
                {
                    Graphics.Blit(tex, renderTexture);
                };
            }
        };

        StartCoroutine(CreateAndSendOffer());
    }

    IEnumerator CreateAndSendOffer()
    {
        var offerOp = pc.CreateOffer();
        yield return offerOp;
        var offer = offerOp.Desc;
        yield return pc.SetLocalDescription(ref offer);

        _ = SendOfferAsync(offer.sdp);
    }

    async Task SendOfferAsync(string sdp)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(gatewayIP, gatewayPort);
        NetworkStream stream = client.GetStream();

        byte[] offerData = Encoding.UTF8.GetBytes(sdp);
        await stream.WriteAsync(offerData, 0, offerData.Length);

        // Recibir answer
        byte[] buffer = new byte[65536];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        string answerSdp = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        var answer = new RTCSessionDescription
        {
            type = RTCSdpType.Answer,
            sdp = answerSdp
        };

        var op = pc.SetRemoteDescription(ref answer);
        while (!op.IsDone) await Task.Yield();
        Debug.Log("Conexión WebRTC establecida");
    }

    void Update()
    {
        WebRTC.Update();
    }

    private void OnDestroy()
    {
        pc?.Close();
        WebRTC.Dispose();
    }
}
