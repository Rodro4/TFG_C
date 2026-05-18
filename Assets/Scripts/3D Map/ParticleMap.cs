// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using RosSharp.RosBridgeClient;
// using RosSharp.RosBridgeClient.MessageTypes.Sensor;

// public class ParticleMap : MonoBehaviour
// {
//     [Header("ROS")]
//     public string PointCloudTopic = "/camera/depth_registered/points_decimated";
//     public bool useROS = true;
//     public int PointSkip = 1; // El decimado ya lo hace el relay en el robot

//     [Header("Particles")]
//     public int MaxPoints = 200000;
//     public float particleSize = 0.05f;

//     [Header("Voxel Map")]
//     public float voxelSize = 0.1f;
//     public int maxVoxels = 200000;
//     public bool generateColliders = false; // Muy costoso, desactivado por defecto
//     public int collidersPerFrame = 20;

//     private readonly object voxelLock = new object();

//     public RobotMotionDetector motionDetector;

//     public struct VoxelPoint
//     {
//         public Vector3 pos;
//         public Color32 col;
//         public bool hasCollider;
//     }

//     private Dictionary<Vector3Int, VoxelPoint> voxels = new();
//     private List<VoxelPoint> cachedPoints = new();
//     private bool cacheDirty = true;
//     private bool renderDirty = false; // Solo re-renderiza si hay puntos nuevos

//     private Queue<Vector3Int> pendingColliders = new();

//     private RosSocket rosSocket;
//     private TfManager tfManager;
//     private string subscriptionId;

//     private ParticleSystem ps;
//     private ParticleSystem.Particle[] particles;

//     private bool initialized = false;

//     void Awake()
//     {
//         InitParticleSystem();
//         motionDetector = FindObjectOfType<RobotMotionDetector>();
//         initialized = true;
//     }

//     void Start()
//     {
//         if (useROS)
//         {
//             var connector = FindObjectOfType<RosConnector>();
//             if (connector != null)
//             {
//                 rosSocket = connector.RosSocket;
//                 tfManager = FindObjectOfType<TfManager>();
//             }
//             else
//             {
//                 useROS = false;
//                 Debug.LogWarning("ParticleMap: RosConnector no encontrado.");
//             }
//         }

//         OnEnable(); // igual que MeshMap
//     }

//     // =========================
//     // SUSCRIPCIÓN DINÁMICA
//     // =========================
//     void OnEnable()
//     {
//         if (!initialized || rosSocket == null || !useROS) return;
//         if (subscriptionId != null) return; // Evita doble suscripción
//         subscriptionId = rosSocket.Subscribe<PointCloud2>(PointCloudTopic, ReceivePointCloud, 0);
//         Debug.Log($"ParticleMap: Suscrito a {PointCloudTopic}");

//         // Si ya teníamos datos, forzar re-render al volver a activarse
//         if (HasData()) renderDirty = true;
//     }

//     void OnDisable()
//     {
//         if (rosSocket == null || subscriptionId == null) return;
//         rosSocket.Unsubscribe(subscriptionId);
//         subscriptionId = null;
//         Debug.Log("ParticleMap: Desuscrito.");
//     }

//     void InitParticleSystem()
//     {
//         ps = GetComponent<ParticleSystem>();
//         if (ps == null)
//             ps = gameObject.AddComponent<ParticleSystem>();

//         var main = ps.main;
//         main.maxParticles = MaxPoints;
//         main.loop = false;
//         main.playOnAwake = false;
//         main.simulationSpace = ParticleSystemSimulationSpace.World;
//         main.startSize = particleSize;
//         main.startLifetime = float.MaxValue;

//         var emission = ps.emission;
//         emission.enabled = false;

//         var renderer = ps.GetComponent<ParticleSystemRenderer>();
//         renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
//         renderer.renderMode = ParticleSystemRenderMode.Billboard;

//         particles = new ParticleSystem.Particle[MaxPoints];
//     }

//     // =========================
//     // ROS RECEIVE — hilo secundario de rosbridge
//     // =========================
//     void ReceivePointCloud(PointCloud2 msg)
//     {
//         if (!useROS) return;

//         if (motionDetector != null && !motionDetector.isStationary) return;

//         lock (voxelLock)
//         {
//             if (voxels.Count >= maxVoxels) return;
//         }

//         int step = (int)msg.point_step;
//         int count = (int)(msg.width * msg.height);

//         int ox = -1, oy = -1, oz = -1, orgb = -1;
//         foreach (var f in msg.fields)
//         {
//             if (f.name == "x")                    ox   = (int)f.offset;
//             if (f.name == "y")                    oy   = (int)f.offset;
//             if (f.name == "z")                    oz   = (int)f.offset;
//             if (f.name == "rgb" || f.name == "rgba") orgb = (int)f.offset;
//         }

//         if (ox == -1 || oy == -1 || oz == -1) return;

//         double cloudTime = msg.header.stamp.secs + msg.header.stamp.nsecs * 1e-9;

//         for (int i = 0; i < count; i += PointSkip)
//         {
//             int idx = i * step;

//             float x = BitConverter.ToSingle(msg.data, idx + ox);
//             float y = BitConverter.ToSingle(msg.data, idx + oy);
//             float z = BitConverter.ToSingle(msg.data, idx + oz);

//             if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;

//             Vector3 ros = new Vector3(-y, z, x);
//             Vector3 p;

//             if (!tfManager.TryTransformPointAtTime(ros, msg.header.frame_id, "map", cloudTime, out p))
//                 continue;

//             Color32 col = Color.white;
//             if (orgb != -1)
//             {
//                 uint rgba = BitConverter.ToUInt32(msg.data, idx + orgb);
//                 col = new Color32(
//                     (byte)((rgba >> 16) & 255),
//                     (byte)((rgba >> 8)  & 255),
//                     (byte)(rgba         & 255),
//                     255
//                 );
//             }

//             AddPoint(p, col);
//         }
//     }

//     // =========================
//     // VOXEL ADD — thread-safe
//     // =========================
//     public void AddPoint(Vector3 pos, Color32 col)
//     {
//         Vector3Int key = new Vector3Int(
//             Mathf.FloorToInt(pos.x / voxelSize),
//             Mathf.FloorToInt(pos.y / voxelSize),
//             Mathf.FloorToInt(pos.z / voxelSize)
//         );

//         lock (voxelLock)
//         {
//             if (voxels.ContainsKey(key)) return;
//             if (voxels.Count >= maxVoxels) return;

//             VoxelPoint v = new VoxelPoint
//             {
//                 pos = new Vector3(
//                     (key.x + 0.5f) * voxelSize,
//                     (key.y + 0.5f) * voxelSize,
//                     (key.z + 0.5f) * voxelSize),
//                 col = col,
//                 hasCollider = false
//             };

//             voxels[key] = v;

//             if (generateColliders)
//                 pendingColliders.Enqueue(key);

//             cacheDirty = true;
//             renderDirty = true;
//         }
//     }

//     public List<VoxelPoint> GetPoints()
//     {
//         lock (voxelLock)
//         {
//             if (!cacheDirty) return cachedPoints;

//             cachedPoints.Clear();
//             foreach (var v in voxels.Values)
//                 cachedPoints.Add(v);

//             cacheDirty = false;
//             return cachedPoints;
//         }
//     }

//     public bool HasData()
//     {
//         lock (voxelLock) return voxels.Count > 0;
//     }

//     // =========================
//     // UPDATE — hilo principal
//     // =========================
//     void Update()
//     {
//         if (!initialized) return;
        
//         if (renderDirty)
//         {
//             RenderVoxelMap();
//             renderDirty = false;
//         }

//         if (generateColliders)
//             GenerateColliders();
//     }

//     // =========================
//     // PARTICLES RENDER
//     // =========================
//     void RenderVoxelMap()
//     {
//         var pts = GetPoints();
//         int n = Mathf.Min(pts.Count, MaxPoints);

//         for (int i = 0; i < n; i++)
//         {
//             particles[i].position       = pts[i].pos;
//             particles[i].startColor     = pts[i].col;
//             particles[i].startSize      = particleSize;
//             particles[i].remainingLifetime = float.MaxValue;
//         }

//         ps.SetParticles(particles, n);
//     }

//     // =========================
//     // COLLIDERS (opcional, costoso)
//     // =========================
//     void GenerateColliders()
//     {
//         int created = 0;

//         lock (voxelLock)
//         {
//             while (pendingColliders.Count > 0 && created < collidersPerFrame)
//             {
//                 var key = pendingColliders.Dequeue();
//                 if (!voxels.TryGetValue(key, out VoxelPoint v)) continue;
//                 if (v.hasCollider) continue;

//                 GameObject go = new GameObject("VoxelCollider");
//                 go.transform.SetParent(transform);
//                 go.transform.position = v.pos;
//                 go.AddComponent<BoxCollider>().size = Vector3.one * voxelSize;

//                 v.hasCollider = true;
//                 voxels[key] = v;
//                 created++;
//             }
//         }
//     }

//     // =========================
//     // CLEAR
//     // =========================
//     public void ClearAll()
//     {
//         lock (voxelLock)
//         {
//             voxels.Clear();
//             cachedPoints.Clear();
//             cacheDirty = true;
//             renderDirty = false;
//         }

//         ps.Clear();

//         foreach (Transform child in transform)
//             Destroy(child.gameObject);
//     }
// }



using System;
using System.Collections.Generic;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class ParticleMap : MonoBehaviour
{
    [Header("ROS")]
    public string PointCloudTopic = "/camera/depth_registered/points_decimated";
    public bool useROS = true;
    public int PointSkip = 1;

    [Header("Particles")]
    public int MaxPoints = 200000;
    public float particleSize = 0.05f;

    [Header("Voxel Map")]
    public float voxelSize = 0.1f;
    public int maxVoxels = 200000;
    public bool generateColliders = false;
    public int collidersPerFrame = 20;

    private readonly object voxelLock = new object();

    public RobotMotionDetector motionDetector;

    public struct VoxelPoint { public Vector3 pos; public Color32 col; public bool hasCollider; }

    private Dictionary<Vector3Int, VoxelPoint> voxels = new();
    private List<VoxelPoint> cachedPoints = new();
    private bool cacheDirty = true;
    private bool renderDirty = false;

    // ── NUEVO: conteo de cuántas partículas ya están en el array
    // Solo necesitamos hacer SetParticles del bloque completo cuando el PS
    // no tiene aún todos los puntos. Una vez estabilizado solo actualizamos
    // el delta. En la práctica: si hay puntos nuevos, re-set completo pero
    // solo cuando cambia el tamaño (no en cada frame).
    private int lastRenderedCount = 0;

    private Queue<Vector3Int> pendingColliders = new();

    private RosSocket rosSocket;
    private TfManager tfManager;
    private string subscriptionId;

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    private bool initialized = false;

    void Awake()
    {
        InitParticleSystem();
        motionDetector = FindObjectOfType<RobotMotionDetector>();
        initialized = true;
    }

    void Start()
    {
        if (useROS)
        {
            var connector = FindObjectOfType<RosConnector>();
            if (connector != null)
            {
                rosSocket = connector.RosSocket;
                tfManager = FindObjectOfType<TfManager>();
            }
            else { useROS = false; Debug.LogWarning("ParticleMap: RosConnector no encontrado."); }
        }
        OnEnable();
    }

    void OnEnable()
    {
        if (!initialized || rosSocket == null || !useROS) return;
        if (subscriptionId != null) return;
        subscriptionId = rosSocket.Subscribe<PointCloud2>(PointCloudTopic, ReceivePointCloud, 0);
        Debug.Log($"ParticleMap: Suscrito a {PointCloudTopic}");
        if (HasData()) renderDirty = true;
    }

    void OnDisable()
    {
        if (rosSocket == null || subscriptionId == null) return;
        rosSocket.Unsubscribe(subscriptionId);
        subscriptionId = null;
        Debug.Log("ParticleMap: Desuscrito.");
    }

    void InitParticleSystem()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles    = MaxPoints;
        main.loop            = false;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize       = particleSize;
        main.startLifetime   = float.MaxValue;

        var emission = ps.emission;
        emission.enabled = false;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material   = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        particles = new ParticleSystem.Particle[MaxPoints];
    }

    // ─── ROS RECEIVE ────────────────────────────────────────────────
    void ReceivePointCloud(PointCloud2 msg)
    {
        if (!useROS) return;
        if (motionDetector != null && !motionDetector.isStationary) return;

        lock (voxelLock) { if (voxels.Count >= maxVoxels) return; }

        int step  = (int)msg.point_step;
        int count = (int)(msg.width * msg.height);

        int ox = -1, oy = -1, oz = -1, orgb = -1;
        foreach (var f in msg.fields)
        {
            if (f.name == "x")               ox   = (int)f.offset;
            if (f.name == "y")               oy   = (int)f.offset;
            if (f.name == "z")               oz   = (int)f.offset;
            if (f.name == "rgb" || f.name == "rgba") orgb = (int)f.offset;
        }
        if (ox == -1 || oy == -1 || oz == -1) return;

        double cloudTime = msg.header.stamp.secs + msg.header.stamp.nsecs * 1e-9;

        for (int i = 0; i < count; i += PointSkip)
        {
            int idx = i * step;
            float x = BitConverter.ToSingle(msg.data, idx + ox);
            float y = BitConverter.ToSingle(msg.data, idx + oy);
            float z = BitConverter.ToSingle(msg.data, idx + oz);

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;

            Vector3 ros = new Vector3(-y, z, x);
            if (!tfManager.TryTransformPointAtTime(ros, msg.header.frame_id, "map", cloudTime, out Vector3 p))
                continue;

            Color32 col = Color.white;
            if (orgb != -1)
            {
                uint rgba = BitConverter.ToUInt32(msg.data, idx + orgb);
                col = new Color32((byte)((rgba >> 16) & 255), (byte)((rgba >> 8) & 255), (byte)(rgba & 255), 255);
            }

            AddPoint(p, col);
        }
    }

    // ─── VOXEL ADD ──────────────────────────────────────────────────
    public void AddPoint(Vector3 pos, Color32 col)
    {
        Vector3Int key = new Vector3Int(
            Mathf.FloorToInt(pos.x / voxelSize),
            Mathf.FloorToInt(pos.y / voxelSize),
            Mathf.FloorToInt(pos.z / voxelSize));

        lock (voxelLock)
        {
            if (voxels.ContainsKey(key)) return;
            if (voxels.Count >= maxVoxels) return;

            voxels[key] = new VoxelPoint
            {
                pos = new Vector3((key.x + 0.5f) * voxelSize, (key.y + 0.5f) * voxelSize, (key.z + 0.5f) * voxelSize),
                col = col,
                hasCollider = false
            };

            if (generateColliders) pendingColliders.Enqueue(key);

            cacheDirty  = true;
            renderDirty = true;
        }
    }

    public List<VoxelPoint> GetPoints()
    {
        lock (voxelLock)
        {
            if (!cacheDirty) return cachedPoints;
            cachedPoints.Clear();
            foreach (var v in voxels.Values) cachedPoints.Add(v);
            cacheDirty = false;
            return cachedPoints;
        }
    }

    public bool HasData() { lock (voxelLock) return voxels.Count > 0; }

    // ─── UPDATE ─────────────────────────────────────────────────────
    void Update()
    {
        if (!initialized) return;

        if (renderDirty)
        {
            RenderVoxelMap();
            renderDirty = false;
        }

        if (generateColliders) GenerateColliders();
    }

    // ─── PARTICLES RENDER ───────────────────────────────────────────
    // Solo llama a SetParticles si el conteo cambió (hay puntos nuevos).
    // No recorre la caché completa si no hay nada nuevo.
    void RenderVoxelMap()
    {
        var pts = GetPoints();
        int n = Mathf.Min(pts.Count, MaxPoints);

        // Si el número no cambió no hay nada que hacer
        if (n == lastRenderedCount) return;

        // Solo inicializar las partículas NUEVAS (índices lastRenderedCount..n-1)
        for (int i = lastRenderedCount; i < n; i++)
        {
            particles[i].position          = pts[i].pos;
            particles[i].startColor        = pts[i].col;
            particles[i].startSize         = particleSize;
            particles[i].remainingLifetime = float.MaxValue;
        }

        ps.SetParticles(particles, n);
        lastRenderedCount = n;
    }

    // ─── COLLIDERS ──────────────────────────────────────────────────
    void GenerateColliders()
    {
        int created = 0;
        lock (voxelLock)
        {
            while (pendingColliders.Count > 0 && created < collidersPerFrame)
            {
                var key = pendingColliders.Dequeue();
                if (!voxels.TryGetValue(key, out VoxelPoint v)) continue;
                if (v.hasCollider) continue;

                GameObject go = new GameObject("VoxelCollider");
                go.transform.SetParent(transform);
                go.transform.position = v.pos;
                go.AddComponent<BoxCollider>().size = Vector3.one * voxelSize;

                v.hasCollider = true;
                voxels[key] = v;
                created++;
            }
        }
    }

    // ─── CLEAR ──────────────────────────────────────────────────────
    public void ClearAll()
    {
        lock (voxelLock)
        {
            voxels.Clear();
            cachedPoints.Clear();
            cacheDirty  = true;
            renderDirty = false;
        }

        lastRenderedCount = 0;
        ps.Clear();

        foreach (Transform child in transform) Destroy(child.gameObject);
    }
}