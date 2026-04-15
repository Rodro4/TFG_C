//using System;
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Sensor;
//using System.Collections.Generic;

//public class PointCloud2Subscriber : MonoBehaviour
//{
//    public string PointCloudTopic = "/camera/depth/points";
//    public bool useROS = true;

//    public int MaxPoints = 400000;
//    public int PointSkip = 10;

//    private RosSocket rosSocket;
//    private ParticleSystem ps;
//    private ParticleSystem.Particle[] particles;

//    private TfManager tfManager;
//    private PersistentVoxelMap voxelMap;

//    private bool initialized = false;

//    void Start()
//    {
//        voxelMap = FindObjectOfType<PersistentVoxelMap>();
//        InitParticleSystem();

//        if (useROS)
//        {
//            var connector = FindObjectOfType<RosConnector>();

//            if (connector != null)
//            {
//                rosSocket = connector.RosSocket;
//                tfManager = FindObjectOfType<TfManager>();

//                rosSocket.Subscribe<PointCloud2>(PointCloudTopic, ReceivePointCloud, 0);
//            }
//            else
//            {
//                useROS = false;
//            }
//        }

//        initialized = true;
//    }

//    // =========================
//    // INIT PARTICLES
//    // =========================
//    void InitParticleSystem()
//    {
//        ps = GetComponent<ParticleSystem>();
//        if (ps == null)
//            ps = gameObject.AddComponent<ParticleSystem>();

//        var main = ps.main;
//        main.maxParticles = MaxPoints;
//        main.loop = false;
//        main.playOnAwake = false;
//        main.simulationSpace = ParticleSystemSimulationSpace.World;
//        main.startSize = 0.05f;

//        var emission = ps.emission;
//        emission.enabled = false;

//        var renderer = ps.GetComponent<ParticleSystemRenderer>();
//        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

//        //  CLAVE: QUE MIRE A CÁMARA
//        renderer.renderMode = ParticleSystemRenderMode.Billboard;

//        particles = new ParticleSystem.Particle[MaxPoints];
//    }

//    // =========================
//    // ROS INPUT
//    // =========================
//    void ReceivePointCloud(PointCloud2 msg)
//    {
//        if (!useROS || voxelMap == null || tfManager == null)
//            return;

//        int step = (int)msg.point_step;
//        int count = (int)(msg.width * msg.height);

//        int ox = -1, oy = -1, oz = -1, orgb = -1;

//        foreach (var f in msg.fields)
//        {
//            if (f.name == "x") ox = (int)f.offset;
//            if (f.name == "y") oy = (int)f.offset;
//            if (f.name == "z") oz = (int)f.offset;
//            if (f.name == "rgb" || f.name == "rgba") orgb = (int)f.offset;
//        }

//        for (int i = 0; i < count; i += PointSkip)
//        {
//            int idx = i * step;

//            float x = BitConverter.ToSingle(msg.data, idx + ox);
//            float y = BitConverter.ToSingle(msg.data, idx + oy);
//            float z = BitConverter.ToSingle(msg.data, idx + oz);

//            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;

//            Vector3 pRos = new Vector3(-y, z, x);

//            if (!tfManager.TryTransformPoint(
//                pRos,
//                msg.header.frame_id,
//                "map",
//                out Vector3 pMapRos))
//                continue;

//            Vector3 pUnity = new Vector3(pMapRos.x, pMapRos.y, pMapRos.z);

//            Color32 col = Color.white;

//            if (orgb != -1)
//            {
//                uint rgba = BitConverter.ToUInt32(msg.data, idx + orgb);
//                col = new Color32(
//                    (byte)((rgba >> 16) & 255),
//                    (byte)((rgba >> 8) & 255),
//                    (byte)(rgba & 255),
//                    255
//                );
//            }

//            voxelMap.AddPoint(pUnity, col);
//        }
//    }

//    // =========================
//    // UPDATE
//    // =========================
//    void Update()
//    {
//        if (!initialized || voxelMap == null)
//            return;

//        if (voxelMap.HasData())
//            RenderVoxelMap();
//    }

//    // =========================
//    // RENDER
//    // =========================
//    public void RenderVoxelMap()
//    {
//        List<PersistentVoxelMap.VoxelPoint> pts = voxelMap.GetPoints();

//        int n = Mathf.Min(pts.Count, MaxPoints);

//        for (int i = 0; i < n; i++)
//        {
//            particles[i].position = pts[i].pos;
//            particles[i].startColor = pts[i].col;
//            particles[i].startSize = 0.05f;
//            particles[i].remainingLifetime = float.MaxValue;
//        }

//        ps.SetParticles(particles, n);
//    }
//}










































//using System;
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Sensor;

//public class PointCloud2Subscriber : MonoBehaviour
//{
//    public string topic = "/camera/depth/points";
//    public bool useROS = true;

//    public int skip = 10;

//    private RosSocket socket;
//    private TfManager tf;
//    private PersistentVoxelMap map;

//    private Color32[] colorsCache;
//    private Vector3[] posCache;

//    void Start()
//    {
//        map = FindObjectOfType<PersistentVoxelMap>();

//        if (useROS)
//        {
//            var conn = FindObjectOfType<RosConnector>();
//            if (conn == null) return;

//            socket = conn.RosSocket;
//            tf = FindObjectOfType<TfManager>();

//            socket.Subscribe<PointCloud2>(topic, Receive, 0);
//        }
//    }

//    void Receive(PointCloud2 msg)
//    {
//        int step = (int)msg.point_step;
//        int count = (int)(msg.width * msg.height);

//        int ox = -1, oy = -1, oz = -1, oc = -1;

//        foreach (var f in msg.fields)
//        {
//            if (f.name == "x") ox = (int)f.offset;
//            if (f.name == "y") oy = (int)f.offset;
//            if (f.name == "z") oz = (int)f.offset;
//            if (f.name == "rgb") oc = (int)f.offset;
//        }

//        for (int i = 0; i < count; i += skip)
//        {
//            int idx = i * step;

//            float x = BitConverter.ToSingle(msg.data, idx + ox);
//            float y = BitConverter.ToSingle(msg.data, idx + oy);
//            float z = BitConverter.ToSingle(msg.data, idx + oz);

//            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;

//            Vector3 ros = new Vector3(-y, z, x);

//            if (!tf.TryTransformPoint(ros, msg.header.frame_id, "map", out var p))
//                continue;

//            Color32 c = Color.white;

//            if (oc != -1)
//            {
//                uint rgb = BitConverter.ToUInt32(msg.data, idx + oc);
//                c = new Color32(
//                    (byte)((rgb >> 16) & 255),
//                    (byte)((rgb >> 8) & 255),
//                    (byte)(rgb & 255),
//                    255
//                );
//            }

//            map.AddPoint(p, c);
//        }
//    }

//    void Update()
//    {
//        // SOLO update mesh indirecto
//        if (map != null && map.HasData())
//            return;
//    }
//}