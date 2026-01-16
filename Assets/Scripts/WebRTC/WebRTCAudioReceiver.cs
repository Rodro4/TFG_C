using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Unity.WebRTC;

public class WebRTCAudioReceiver : MonoBehaviour
{
    [Header("Gateway WebRTC URL")]
    public string signalingUrl = "http://172.17.189.225:8080/offer";

    private RTCPeerConnection pc;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        WebRTC.Initialize();
        StartCoroutine(StartWebRTC());
    }

    IEnumerator StartWebRTC()
    {
        RTCConfiguration config = new RTCConfiguration
        {
            iceServers = new RTCIceServer[] { }
        };

        pc = new RTCPeerConnection(ref config);

        //  ESTO ES LO QUE FALTABA
        pc.AddTransceiver(TrackKind.Audio, new RTCRtpTransceiverInit
        {
            direction = RTCRtpTransceiverDirection.RecvOnly
        });

        pc.OnTrack = e =>
        {
            if (e.Track is AudioStreamTrack audioTrack)
            {
                Debug.Log("WebRTCAudioReceiver: Audio track recibido");
                audioSource.SetTrack(audioTrack);
                audioSource.Play();
            }
        };

        var offerOp = pc.CreateOffer();
        yield return offerOp;

        var offer = offerOp.Desc;
        yield return pc.SetLocalDescription(ref offer);

        yield return SendOffer(offer);
    }


    IEnumerator SendOffer(RTCSessionDescription offer)
    {
        string json = JsonUtility.ToJson(new SDPMessage
        {
            sdp = offer.sdp,
            type = offer.type.ToString().ToLower()
        });

        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(signalingUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("WebRTCAudioReceiver: Error WebRTC: " + request.error);
                yield break;
            }

            var answer = JsonUtility.FromJson<SDPMessage>(request.downloadHandler.text);

            RTCSessionDescription answerDesc = new RTCSessionDescription
            {
                sdp = answer.sdp,
                type = RTCSdpType.Answer
            };

            yield return pc.SetRemoteDescription(ref answerDesc);
        }
    }

    void OnDestroy()
    {
        pc?.Close();
        WebRTC.Dispose();
    }

    [Serializable]
    public class SDPMessage
    {
        public string sdp;
        public string type;
    }
}
