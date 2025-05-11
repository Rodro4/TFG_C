using UnityEngine;
using System;

namespace RosSharp.RosBridgeClient
{
    [RequireComponent(typeof(RosConnector))]
    public class DepthImageSubscriber : UnitySubscriber<MessageTypes.Sensor.Image>
    {
        public MeshRenderer meshRenderer;

        private Texture2D texture2D;
        private byte[] imageData;
        private bool isMessageReceived;

        private int imageWidth;
        private int imageHeight;

        private const float maxDepthMeters = 10.0f; // Puedes ajustarlo según tu sensor

        protected override void Start()
        {
            base.Start();
            meshRenderer.material = new Material(Shader.Find("Unlit/Texture"));
        }

        private void Update()
        {
            if (isMessageReceived)
                ProcessMessage();
        }

        protected override void ReceiveMessage(MessageTypes.Sensor.Image image)
        {
            imageData = image.data;
            imageWidth = (int)image.width;
            imageHeight = (int)image.height;
            isMessageReceived = true;
        }

        private void ProcessMessage()
        {
            if (imageData == null || imageData.Length == 0)
                return;

            int pixelCount = imageWidth * imageHeight;

            if (texture2D == null || texture2D.width != imageWidth || texture2D.height != imageHeight)
            {
                texture2D = new Texture2D(imageWidth, imageHeight, TextureFormat.RGBA32, false);
            }

            Color32[] pixels = new Color32[pixelCount];

            for (int i = 0; i < pixelCount; i++)
            {
                int byteIndex = i * 4;
                if (byteIndex + 3 >= imageData.Length)
                    break;

                float depth = BitConverter.ToSingle(imageData, byteIndex);

                // Evitamos NaN o valores negativos
                if (float.IsNaN(depth) || depth <= 0f || depth > maxDepthMeters)
                    depth = maxDepthMeters;

                float normalized = Mathf.Clamp01(depth / maxDepthMeters);
                byte gray = (byte)(normalized * 255);
                pixels[i] = new Color32(gray, gray, gray, 255);
            }

            texture2D.SetPixels32(pixels);
            texture2D.Apply();

            meshRenderer.material.mainTexture = texture2D;
            isMessageReceived = false;
        }
    }
}
