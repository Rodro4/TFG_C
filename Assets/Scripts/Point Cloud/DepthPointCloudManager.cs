////Crea puntos en el espacio
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Sensor;
//using System;

//public class DepthPointCloudManager : MonoBehaviour
//{
//    public string depthTopic = "/camera/depth/image_raw";
//    public string rgbTopic = "/camera/rgb/image_raw";

//    private RosSocket rosSocket;
//    private bool depthReady = false, rgbReady = false;

//    private float[,] depthData;
//    private Color32[] rgbData;
//    private int imageWidth = 640;
//    private int imageHeight = 480;

//    Parámetros típicos de cámara
//    private float fx = 525f;
//    private float fy = 525f;
//    private float cx = 319.5f;
//    private float cy = 239.5f;

//    void Start()
//    {
//        RosConnector rosConnector = FindObjectOfType<RosConnector>();
//        if (rosConnector == null)
//        {
//            Debug.LogError("RosConnector no encontrado.");
//            return;
//        }

//        rosSocket = rosConnector.RosSocket;

//        rosSocket.Subscribe<Image>(depthTopic, ReceiveDepth);
//        rosSocket.Subscribe<Image>(rgbTopic, ReceiveRGB);
//        Debug.Log("Subscripciones hechas a: " + depthTopic + " y " + rgbTopic);
//    }

//    private void ReceiveDepth(Image message)
//    {
//        try
//        {
//            int width = (int)message.width;
//            int height = (int)message.height;

//            if (message.data.Length < width * height * 2)
//            {
//                Debug.LogWarning("Profundidad: datos insuficientes.");
//                return;
//            }

//            depthData = new float[width, height];

//            for (int i = 0; i < message.data.Length; i += 2)
//            {
//                int index = i / 2;
//                int x = index % width;
//                int y = index / width;
//                if (y >= height) continue;

//                ushort depth = BitConverter.ToUInt16(message.data, i);
//                depthData[x, y] = depth * 0.001f; // mm a metros
//            }

//            imageWidth = width;
//            imageHeight = height;
//            depthReady = true;

//            Debug.Log("Profundidad recibida: " + width + "x" + height);
//        }
//        catch (Exception e)
//        {
//            Debug.LogError("Error al procesar la profundidad: " + e.Message);
//        }
//    }

//    private void ReceiveRGB(Image message)
//    {
//        try
//        {
//            int width = (int)message.width;
//            int height = (int)message.height;

//            if (message.data.Length < width * height * 3)
//            {
//                Debug.LogWarning("RGB: datos insuficientes.");
//                return;
//            }

//            rgbData = new Color32[width * height];

//            for (int i = 0; i < rgbData.Length; i++)
//            {
//                int dataIndex = i * 3;
//                byte r = message.data[dataIndex];
//                byte g = message.data[dataIndex + 1];
//                byte b = message.data[dataIndex + 2];
//                rgbData[i] = new Color32(r, g, b, 255);
//            }

//            imageWidth = width;
//            imageHeight = height;
//            rgbReady = true;

//            Debug.Log("RGB recibida: " + width + "x" + height);
//        }
//        catch (Exception e)
//        {
//            Debug.LogError("Error al procesar RGB: " + e.Message);
//        }
//    }

//    void Update()
//    {
//        if (depthReady && rgbReady)
//        {
//            Debug.Log("Construyendo nube de puntos...");
//            BuildPointCloud();
//            depthReady = false;
//            rgbReady = false;
//        }
//    }

//    private void BuildPointCloud()
//    {
//        foreach (Transform child in transform)
//            Destroy(child.gameObject);

//        int step = 8;
//        int pointCount = 0;

//        for (int y = 0; y < imageHeight; y += step)
//        {
//            for (int x = 0; x < imageWidth; x += step)
//            {
//                float z = depthData[x, y];
//                if (z <= 0.1f || z > 4f)
//                    continue;

//                float x3D = (x - cx) * z / fx;
//                float y3D = (y - cy) * z / fy;

//                Vector3 pointPos = new Vector3(x3D, -y3D, z);

//                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//                point.transform.position = pointPos;
//                point.transform.localScale = Vector3.one * 0.03f;
//                point.transform.SetParent(transform);

//                int idx = y * imageWidth + x;
//                if (rgbData != null && idx < rgbData.Length)
//                    point.GetComponent<Renderer>().material.color = rgbData[idx];

//                pointCount++;
//            }
//        }

//        Debug.Log("Nube creada. Total puntos: " + pointCount);
//    }
//}











////Crea una pirámide rellena de puntos que vista desde la punta se ve la imgen RGB del turtlebot2
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Sensor;
//using System;
//using System.Collections.Generic;

//public class DepthPointCloudManager : MonoBehaviour
//{
//    public string depthTopic = "/camera/depth/image_raw";
//    public string rgbTopic = "/camera/rgb/image_raw";

//    public int step = 4;                 // Resolución de muestreo
//    public int maxPoints = 50000;        // Límite de puntos acumulados

//    private RosSocket rosSocket;
//    private bool depthReady = false, rgbReady = false;

//    private float[,] depthData;
//    private Color32[] rgbData;
//    private int imageWidth = 640;
//    private int imageHeight = 480;

//    private float fx = 525f, fy = 525f, cx = 319.5f, cy = 239.5f;

//    private List<Vector3> pointList = new List<Vector3>();
//    private List<Color32> colorList = new List<Color32>();

//    private Mesh mesh;
//    private MeshFilter meshFilter;
//    private MeshRenderer meshRenderer;

//    public Material pointCloudMaterial;  // <--- Exponer para asignar desde Inspector

//    void Start()
//    {
//        var rosConnector = FindObjectOfType<RosConnector>();
//        rosSocket = rosConnector.RosSocket;

//        rosSocket.Subscribe<Image>(depthTopic, ReceiveDepth);
//        rosSocket.Subscribe<Image>(rgbTopic, ReceiveRGB);

//        meshFilter = gameObject.AddComponent<MeshFilter>();
//        meshRenderer = gameObject.AddComponent<MeshRenderer>();

//        if (pointCloudMaterial != null)
//        {
//            meshRenderer.material = pointCloudMaterial;  // asigna el material arrastrado
//        }
//        else
//        {
//            Debug.LogError("Por favor asigna el material PointCloudMaterial en el inspector.");
//        }

//        mesh = new Mesh();
//        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
//        meshFilter.mesh = mesh;
//    }


//    private void ReceiveDepth(Image message)
//    {
//        int width = (int)message.width;
//        int height = (int)message.height;

//        if (message.data.Length < width * height * 2)
//            return;

//        depthData = new float[width, height];

//        for (int i = 0; i < message.data.Length; i += 2)
//        {
//            int index = i / 2;
//            int x = index % width;
//            int y = index / width;
//            if (y >= height) continue;

//            ushort depth = BitConverter.ToUInt16(message.data, i);
//            depthData[x, y] = depth * 0.001f; // mm a metros
//        }

//        imageWidth = width;
//        imageHeight = height;
//        depthReady = true;
//    }

//    private void ReceiveRGB(Image message)
//    {
//        int width = (int)message.width;
//        int height = (int)message.height;

//        if (message.data.Length < width * height * 3)
//            return;

//        rgbData = new Color32[width * height];

//        for (int i = 0; i < rgbData.Length; i++)
//        {
//            int dataIndex = i * 3;
//            byte r = message.data[dataIndex];
//            byte g = message.data[dataIndex + 1];
//            byte b = message.data[dataIndex + 2];
//            rgbData[i] = new Color32(r, g, b, 255);
//        }

//        imageWidth = width;
//        imageHeight = height;
//        rgbReady = true;
//    }

//    void Update()
//    {
//        if (depthReady && rgbReady)
//        {
//            AddPoints();
//            UpdateMesh();
//            depthReady = false;
//            rgbReady = false;
//        }
//    }

//    private void AddPoints()
//    {
//        for (int y = 0; y < imageHeight; y += step)
//        {
//            for (int x = 0; x < imageWidth; x += step)
//            {
//                float z = depthData[x, y];
//                if (z <= 0.1f || z > 5f) continue;

//                float x3D = (x - cx) * z / fx;
//                float y3D = (y - cy) * z / fy;

//                Vector3 pos = new Vector3(x3D, -y3D, z);
//                pointList.Add(pos);

//                int idx = y * imageWidth + x;
//                Color32 color = rgbData != null && idx < rgbData.Length ? rgbData[idx] : Color.white;
//                colorList.Add(color);
//            }
//        }

//        // Límite de puntos
//        if (pointList.Count > maxPoints)
//        {
//            int excess = pointList.Count - maxPoints;
//            pointList.RemoveRange(0, excess);
//            colorList.RemoveRange(0, excess);
//        }
//    }

//    private void UpdateMesh()
//    {
//        mesh.Clear();
//        mesh.SetVertices(pointList);
//        mesh.SetColors(colorList);

//        int[] indices = new int[pointList.Count];
//        for (int i = 0; i < indices.Length; i++) indices[i] = i;

//        mesh.SetIndices(indices, MeshTopology.Points, 0);
//        meshFilter.mesh = mesh;
//    }

//    // Llamable externamente para limpiar
//    public void ClearMap()
//    {
//        pointList.Clear();
//        colorList.Clear();
//        mesh.Clear();
//    }
//}












