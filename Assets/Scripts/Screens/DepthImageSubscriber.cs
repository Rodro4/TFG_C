using UnityEngine;
using System;
using RosSharp.RosBridgeClient;

namespace RosSharp.RosBridgeClient
{
    [RequireComponent(typeof(RosConnector))]
    public class DepthImageSubscriber : UnitySubscriber<MessageTypes.Sensor.Image>
    {
        public MeshRenderer meshRenderer;

        private Texture2D texture2D;
        private byte[] imageData;
        private float[] depthValues;

        private int imageWidth;
        private int imageHeight;

        private bool isMessageReceived = false;
        private const float maxDepthMeters = 10.0f;

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

            int pixelCount = imageWidth * imageHeight;
            depthValues = new float[pixelCount];

            for (int i = 0; i < pixelCount; i++)
            {
                int byteIndex = i * 4;
                if (byteIndex + 3 >= imageData.Length) break;

                float depth = BitConverter.ToSingle(imageData, byteIndex);
                depthValues[i] = depth;
            }

            isMessageReceived = true;
        }

        private void ProcessMessage()
        {
            if (depthValues == null || depthValues.Length == 0)
                return;

            if (texture2D == null || texture2D.width != imageWidth || texture2D.height != imageHeight)
                texture2D = new Texture2D(imageWidth, imageHeight, TextureFormat.RGBA32, false);

            Color32[] pixels = new Color32[depthValues.Length];

            for (int i = 0; i < depthValues.Length; i++)
            {
                float depth = depthValues[i];

                if (float.IsNaN(depth) || depth <= 0f || depth > maxDepthMeters)
                    depth = maxDepthMeters;

                float normalized = Mathf.Clamp01(depth / maxDepthMeters);
                byte gray = (byte)(normalized * 255f);
                pixels[i] = new Color32(gray, gray, gray, 255);
            }

            texture2D.SetPixels32(pixels);
            texture2D.Apply();
            meshRenderer.material.mainTexture = texture2D;

            isMessageReceived = false;
        }

        public float GetDepthAt(int x, int y)
        {
            if (depthValues == null || x < 0 || x >= imageWidth || y < 0 || y >= imageHeight)
                return -1f;

            int index = y * imageWidth + x;
            return depthValues[index];
        }
    }
}
