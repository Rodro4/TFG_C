// PointCloud2 en tiempo real, sin persistencia ni acumulación de puntos
//using System;
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Sensor;

//public class PointCloud2ParticleSubscriber : MonoBehaviour
//{
//    public string PointCloudTopic = "/camera/depth/points";

//    [Tooltip("Máximo número absoluto de puntos a mostrar")]
//    public int MaxPoints = 400000;

//    [Tooltip("Salto para muestrear puntos. 1 = usar todos, 2 = 1 de cada 2, etc.")]
//    public int PointSkip = 10;

//    private RosSocket rosSocket;
//    private ParticleSystem ParticleSystem;
//    private ParticleSystem.Particle[] particles;

//    private Vector3[] positions;
//    private Color32[] colors;

//    private int currentPointCount = 0;

//    private TfManager tfManager;

//    void Start()
//    {
//        var rosConnector = FindObjectOfType<RosConnector>();
//        if (rosConnector == null)
//        {
//            Debug.LogError("RosConnector no encontrado.");
//            enabled = false;
//            return;
//        }

//        rosSocket = rosConnector.RosSocket;
//        rosSocket.Subscribe<PointCloud2>(PointCloudTopic, ReceivePointCloud, 0);

//        ParticleSystem = gameObject.AddComponent<ParticleSystem>();

//        var main = ParticleSystem.main;
//        main.maxParticles = MaxPoints;
//        main.loop = false;
//        main.playOnAwake = false;
//        main.simulationSpace = ParticleSystemSimulationSpace.World;
//        main.startSize = 0.05f;

//        var emission = ParticleSystem.emission;
//        emission.enabled = false;

//        var renderer = ParticleSystem.GetComponent<ParticleSystemRenderer>();
//        Material particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
//        particleMaterial.SetColor("_Color", Color.white);
//        renderer.material = particleMaterial;

//        particles = new ParticleSystem.Particle[MaxPoints];
//        positions = new Vector3[MaxPoints];
//        colors = new Color32[MaxPoints];

//        tfManager = FindObjectOfType<TfManager>();
//    }

//    private void ReceivePointCloud(PointCloud2 msg)
//    {
//        int pointStep = (int)msg.point_step;
//        int width = (int)msg.width;
//        int height = (int)msg.height;

//        int xOffset = -1, yOffset = -1, zOffset = -1, rgbOffset = -1;
//        foreach (var field in msg.fields)
//        {
//            if (field.name == "x") xOffset = (int)field.offset;
//            if (field.name == "y") yOffset = (int)field.offset;
//            if (field.name == "z") zOffset = (int)field.offset;
//            if (field.name == "rgb" || field.name == "rgba") rgbOffset = (int)field.offset;
//        }

//        if (xOffset == -1 || yOffset == -1 || zOffset == -1)
//        {
//            Debug.LogWarning("No se encontraron campos XYZ en PointCloud2.");
//            return;
//        }

//        int totalPoints = width * height;
//        int maxProcessPoints = MaxPoints * PointSkip;

//        int validPoints = 0;

//        for (int i = 0; i < totalPoints && validPoints < MaxPoints; i += PointSkip)
//        {
//            if (i >= maxProcessPoints) break;

//            int baseIndex = i * pointStep;

//            float x = BitConverter.ToSingle(msg.data, baseIndex + xOffset);
//            float y = BitConverter.ToSingle(msg.data, baseIndex + yOffset);
//            float z = BitConverter.ToSingle(msg.data, baseIndex + zOffset);

//            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
//                continue;

//            if (z <= 0.01f || z > 10f)
//                continue;



//            Vector3 pRos = new Vector3(-y, z, x);


//            if (tfManager.TryTransformPoint(
//    pRos,
//    msg.header.frame_id,
//    "odom",
//    out Vector3 pOdomRos)
//)
//            {
//                Vector3 pUnity = new Vector3(
//    pOdomRos.x,
//    pOdomRos.y,
//    pOdomRos.z
//);


//                positions[validPoints] = pUnity;
//            }

//            else
//            {
//                continue;
//            }





//            Color32 col = new Color32(255, 255, 255, 255);

//            if (rgbOffset != -1 && msg.data.Length >= baseIndex + rgbOffset + 4)
//            {
//                uint rgba = BitConverter.ToUInt32(msg.data, baseIndex + rgbOffset);
//                byte r = (byte)((rgba >> 16) & 0xFF);
//                byte g = (byte)((rgba >> 8) & 0xFF);
//                byte b = (byte)(rgba & 0xFF);
//                col = new Color32(r, g, b, 255);
//            }

//            colors[validPoints] = col;

//            validPoints++;
//        }

//        currentPointCount = validPoints;
//    }

//    void Update()
//    {
//        if (currentPointCount == 0)
//            return;

//        for (int i = 0; i < currentPointCount; i++)
//        {
//            particles[i].position = positions[i];
//            particles[i].startColor = colors[i];
//            particles[i].startSize = 0.05f;
//            particles[i].remainingLifetime = 1f;
//        }

//        ParticleSystem.SetParticles(particles, currentPointCount);
//    }
//}










using System;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class PointCloud2ParticleSubscriber : MonoBehaviour
{
    public string PointCloudTopic = "/camera/depth/points";
    public int MaxPoints = 400000;
    public int PointSkip = 10;

    private RosSocket rosSocket;
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    private TfManager tfManager;
    private PersistentVoxelMap voxelMap;

    void Start()
    {
        rosSocket = FindObjectOfType<RosConnector>().RosSocket;
        tfManager = FindObjectOfType<TfManager>();
        voxelMap = FindObjectOfType<PersistentVoxelMap>();

        rosSocket.Subscribe<PointCloud2>(PointCloudTopic, ReceivePointCloud, 0);

        ps = gameObject.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.maxParticles = MaxPoints;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize = 0.05f;

        var aux = ps.emission;
        aux.enabled = false;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = new Material(Shader.Find("Particles/Standard Unlit"));

        particles = new ParticleSystem.Particle[MaxPoints];
    }

    void ReceivePointCloud(PointCloud2 msg)
    {
        int step = (int)msg.point_step;
        int count = (int)(msg.width * msg.height);

        int ox = -1, oy = -1, oz = -1, orgb = -1;
        foreach (var f in msg.fields)
        {
            if (f.name == "x") ox = (int)f.offset;
            if (f.name == "y") oy = (int)f.offset;
            if (f.name == "z") oz = (int)f.offset;
            if (f.name == "rgb" || f.name == "rgba") orgb = (int)f.offset;
        }

        for (int i = 0; i < count; i += PointSkip)
        {
            int idx = i * step;

            float x = BitConverter.ToSingle(msg.data, idx + ox);
            float y = BitConverter.ToSingle(msg.data, idx + oy);
            float z = BitConverter.ToSingle(msg.data, idx + oz);

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;
            if (z < 0.1f || z > 10f) continue;

            // optical -> ROS estándar
            Vector3 pRos = new Vector3(-y, z, x);

            if (!tfManager.TryTransformPoint(
                pRos,
                msg.header.frame_id,
                "map",
                out Vector3 pMapRos))
                continue;

            // ROS -> Unity
            Vector3 pUnity = new Vector3(
                pMapRos.x,
                pMapRos.y,
                pMapRos.z
            );

            Color32 col = Color.white;
            if (orgb != -1)
            {
                uint rgba = BitConverter.ToUInt32(msg.data, idx + orgb);
                col = new Color32(
                    (byte)((rgba >> 16) & 255),
                    (byte)((rgba >> 8) & 255),
                    (byte)(rgba & 255),
                    255
                );
            }

            voxelMap.AddPoint(pUnity, col);
        }
    }

    void Update()
    {
        if (!voxelMap.HasNewData()) return;

        var pts = voxelMap.GetAllPoints();
        int n = Mathf.Min(pts.Length, MaxPoints);

        for (int i = 0; i < n; i++)
        {
            particles[i].position = pts[i].pos;
            particles[i].startColor = pts[i].col;
            particles[i].startSize = 0.05f;
            particles[i].remainingLifetime = 999f;
        }

        ps.SetParticles(particles, n);
        voxelMap.ClearDirtyFlag();
    }
}