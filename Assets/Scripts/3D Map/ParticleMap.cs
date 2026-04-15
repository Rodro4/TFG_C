using System;
using System.Collections.Generic;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class ParticleMap : MonoBehaviour
{
    [Header("ROS")]
    public string PointCloudTopic = "/camera/depth/points";
    public bool useROS = true;
    public int PointSkip = 10;

    [Header("Particles")]
    public int MaxPoints = 400000;
    public float particleSize = 0.05f;

    [Header("Voxel Map")]
    public float voxelSize = 0.1f;
    public bool generateColliders = true;
    public int collidersPerFrame = 50;

    private readonly object voxelLock = new object();

    public struct VoxelPoint
    {
        public Vector3 pos;
        public Color32 col;
        public bool hasCollider;
    }

    private Dictionary<Vector3Int, VoxelPoint> voxels = new();
    private List<VoxelPoint> cachedPoints = new();
    private bool cacheDirty = true;

    private Queue<Vector3Int> pendingColliders = new();

    // ROS
    private RosSocket rosSocket;
    private TfManager tfManager;

    // Particles
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    private bool initialized = false;

    // =========================
    // INIT
    // =========================
    void Start()
    {
        InitParticleSystem();

        if (useROS)
        {
            var connector = FindObjectOfType<RosConnector>();

            if (connector != null)
            {
                rosSocket = connector.RosSocket;
                tfManager = FindObjectOfType<TfManager>();

                rosSocket.Subscribe<PointCloud2>(PointCloudTopic, ReceivePointCloud, 0);
            }
            else
            {
                useROS = false;
            }
        }

        initialized = true;
    }

    void InitParticleSystem()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null)
            ps = gameObject.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = MaxPoints;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize = particleSize;

        var emission = ps.emission;
        emission.enabled = false;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        particles = new ParticleSystem.Particle[MaxPoints];
    }

    // =========================
    // ROS RECEIVE
    // =========================
    void ReceivePointCloud(PointCloud2 msg)
    {
        if (!useROS)
            return;

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

            Vector3 ros = new Vector3(-y, z, x);

            // SIEMPRE inicializado
            Vector3 p = ros;

            if (tfManager != null)
            {
                if (!tfManager.TryTransformPoint(
                    ros,
                    msg.header.frame_id,
                    "map",
                    out p))
                    continue;
            }

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

            AddPoint(p, col);
        }
    }

    // =========================
    // VOXEL ADD
    // =========================
    public void AddPoint(Vector3 pos, Color32 col)
    {
        Vector3Int key = new Vector3Int(
            Mathf.FloorToInt(pos.x / voxelSize),
            Mathf.FloorToInt(pos.y / voxelSize),
            Mathf.FloorToInt(pos.z / voxelSize)
        );

        lock (voxelLock)
        {
            if (voxels.ContainsKey(key))
                return;

            VoxelPoint v = new VoxelPoint
            {
                pos = new Vector3(
                    (key.x + 0.5f) * voxelSize,
                    (key.y + 0.5f) * voxelSize,
                    (key.z + 0.5f) * voxelSize),
                col = col,
                hasCollider = false
            };

            voxels[key] = v;

            if (generateColliders)
                pendingColliders.Enqueue(key);

            cacheDirty = true;
        }
    }

    public List<VoxelPoint> GetPoints()
    {
        lock (voxelLock)
        {
            if (!cacheDirty)
                return cachedPoints;

            cachedPoints.Clear();

            foreach (var v in voxels.Values)
                cachedPoints.Add(v);

            cacheDirty = false;
            return cachedPoints;
        }
    }

    public bool HasData()
    {
        lock (voxelLock)
            return voxels.Count > 0;
    }

    // =========================
    // UPDATE
    // =========================
    void Update()
    {
        if (!initialized)
            return;

        if (HasData())
            RenderVoxelMap();

        GenerateColliders();
    }

    // =========================
    // PARTICLES RENDER
    // =========================
    void RenderVoxelMap()
    {
        var pts = GetPoints();
        int n = Mathf.Min(pts.Count, MaxPoints);

        for (int i = 0; i < n; i++)
        {
            particles[i].position = pts[i].pos;
            particles[i].startColor = pts[i].col;
            particles[i].startSize = particleSize;
            particles[i].remainingLifetime = float.MaxValue;
        }

        ps.SetParticles(particles, n);
    }

    // =========================
    // COLLIDERS
    // =========================
    void GenerateColliders()
    {
        int created = 0;

        lock (voxelLock)
        {
            while (pendingColliders.Count > 0 && created < collidersPerFrame)
            {
                var key = pendingColliders.Dequeue();

                if (!voxels.TryGetValue(key, out VoxelPoint v))
                    continue;

                if (v.hasCollider)
                    continue;

                GameObject go = new GameObject("VoxelCollider");
                go.transform.SetParent(transform);
                go.transform.position = v.pos;

                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.size = Vector3.one * voxelSize;

                v.hasCollider = true;
                voxels[key] = v;

                created++;
            }
        }
    }

    // =========================
    // CLEAR
    // =========================
    public void ClearAll()
    {
        lock (voxelLock)
        {
            voxels.Clear();
            cachedPoints.Clear();
            cacheDirty = true;
        }

        foreach (Transform child in transform)
            Destroy(child.gameObject);
    }
}