using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshMap : MonoBehaviour
{
    [Header("Voxel")]
    public float voxelSize = 0.1f;

    [Header("Mesh")]
    public bool generateMesh = true;
    public float meshUpdateInterval = 1.0f;
    public int maxVoxels = 50000;

    [Header("Material")]
    public Material voxelMaterial;

    [Header("ROS")]
    public string PointCloudTopic = "/camera/depth_registered/points_decimated";
    public bool useROS = true;
    public int skip = 1; // El decimado ya lo hace el relay en el robot

    [Header("Collider")]
    public bool enableCollider = false; // Desactiva si no necesitas colisiones en runtime

    private readonly object voxelLock = new object();

    public RobotMotionDetector motionDetector;

    public struct VoxelPoint
    {
        public Vector3 pos;
        public Color32 col;
    }

    private Dictionary<Vector3Int, VoxelPoint> voxels = new();
    private List<VoxelPoint> cached = new();

    private bool dirty = false;
    private float timer = 0f;
    private bool needsColliderUpdate = false;

    private Mesh mesh;
    private MeshCollider meshCollider;

    private List<Vector3> verts = new();
    private List<Color32> cols = new();
    private List<int> tris = new();

    private RosSocket rosSocket;
    private TfManager tfManager;
    private string subscriptionId;

    private bool initialized = false;

    
    void Awake()
    {
        // Solo inicialización local, sin dependencias externas
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        GetComponent<MeshFilter>().mesh = mesh;

        var mr = GetComponent<MeshRenderer>();
        if (voxelMaterial != null)
            mr.material = voxelMaterial;
        else
            Debug.LogWarning("MeshMap: Asigna voxelMaterial.");

        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.enabled = enableCollider;

        motionDetector = FindObjectOfType<RobotMotionDetector>();
    }

    void Start()
    {
        // ROS aquí: Start() garantiza que RosConnector ya está inicializado
        if (useROS)
        {
            var connector = FindObjectOfType<RosConnector>();
            if (connector != null)
            {
                rosSocket = connector.RosSocket;
                tfManager = FindObjectOfType<TfManager>();
            }
            else
            {
                useROS = false;
                Debug.LogWarning("MeshMap: RosConnector no encontrado.");
            }
        }

        initialized = true;

        // Suscribirse si el objeto está activo (Start() solo se llama si está activo)
        // Si está inactivo al inicio, OnEnable() lo hará cuando se active
        OnEnable();
    }

    // =========================
    // SUSCRIPCIÓN DINÁMICA
    // =========================
    void OnEnable()
    {
        if (!initialized || rosSocket == null || !useROS) return;
        if (subscriptionId != null) return; // Evita doble suscripción
        subscriptionId = rosSocket.Subscribe<PointCloud2>(PointCloudTopic, ReceivePointCloud, 0);
        Debug.Log($"MeshMap: Suscrito a {PointCloudTopic}");

        Debug.Log($"MeshMap OnEnable: initialized={initialized}, rosSocket={rosSocket != null}, subscriptionId={subscriptionId}");
    }

    void OnDisable()
    {
        if (rosSocket == null || subscriptionId == null) return;
        rosSocket.Unsubscribe(subscriptionId);
        subscriptionId = null;
        Debug.Log("MeshMap: Desuscrito.");
    }

    // =========================
    // ROS RECEIVE — hilo secundario de rosbridge
    // =========================
    void ReceivePointCloud(PointCloud2 msg)
    {
        if (!useROS) return;

        if (motionDetector != null && !motionDetector.isStationary) return;

        lock (voxelLock)
        {
            if (voxels.Count >= maxVoxels) return;
        }

        int step = (int)msg.point_step;
        int count = (int)(msg.width * msg.height);

        int ox = -1, oy = -1, oz = -1, oc = -1;
        foreach (var f in msg.fields)
        {
            if (f.name == "x")   ox = (int)f.offset;
            if (f.name == "y")   oy = (int)f.offset;
            if (f.name == "z")   oz = (int)f.offset;
            if (f.name == "rgb") oc = (int)f.offset;
        }

        if (ox == -1 || oy == -1 || oz == -1) return;

        double cloudTime = msg.header.stamp.secs + msg.header.stamp.nsecs * 1e-9;

        for (int i = 0; i < count; i += skip)
        {
            int idx = i * step;

            float x = BitConverter.ToSingle(msg.data, idx + ox);
            float y = BitConverter.ToSingle(msg.data, idx + oy);
            float z = BitConverter.ToSingle(msg.data, idx + oz);

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;

            Vector3 ros = new Vector3(-y, z, x);
            Vector3 p;

            if (!tfManager.TryTransformPointAtTime(ros, msg.header.frame_id, "map", cloudTime, out p))
                continue;

            Color32 c = Color.white;
            if (oc != -1)
            {
                uint rgb = BitConverter.ToUInt32(msg.data, idx + oc);
                c = new Color32(
                    (byte)((rgb >> 16) & 255),
                    (byte)((rgb >> 8)  & 255),
                    (byte)(rgb         & 255),
                    255
                );
            }

            AddPoint(p, c);
        }
    }

    // =========================
    // VOXELS — thread-safe
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
            if (voxels.ContainsKey(key)) return;
            if (voxels.Count >= maxVoxels) return;

            voxels[key] = new VoxelPoint
            {
                pos = new Vector3(
                    (key.x + 0.5f) * voxelSize,
                    (key.y + 0.5f) * voxelSize,
                    (key.z + 0.5f) * voxelSize),
                col = col
            };

            dirty = true;
        }
    }

    public bool HasData()
    {
        lock (voxelLock) return voxels.Count > 0;
    }

    public List<VoxelPoint> GetPoints()
    {
        lock (voxelLock)
        {
            cached.Clear();
            cached.Capacity = voxels.Count;
            foreach (var v in voxels.Values)
                cached.Add(v);
            return cached;
        }
    }

    public void ClearAll()
    {
        lock (voxelLock)
        {
            voxels.Clear();
            cached.Clear();
        }
        mesh.Clear();
        if (meshCollider != null)
            meshCollider.sharedMesh = null;
    }

    // =========================
    // UPDATE — hilo principal
    // =========================
    void Update()
    {
        if (!initialized) return;

        // Aplicar collider cuando el baking async haya terminado
        if (needsColliderUpdate && enableCollider)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
            needsColliderUpdate = false;
        }

        if (!generateMesh || !dirty) return;

        timer += Time.deltaTime;
        if (timer < meshUpdateInterval) return;

        timer = 0;
        dirty = false;

        RebuildMesh();
    }

    // =========================
    // MESH BUILD
    // =========================
    void RebuildMesh()
    {
        var pts = GetPoints();

        verts.Clear();
        cols.Clear();
        tris.Clear();

        int index = 0;
        foreach (var v in pts)
        {
            AddCube(v.pos, v.col, index);
            index += 8;
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Baking del collider en thread secundario para no bloquear el main thread
        if (enableCollider)
        {
            int meshId = mesh.GetInstanceID();
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                Physics.BakeMesh(meshId, false);
                needsColliderUpdate = true;
            });
        }
    }

    // =========================
    // CUBE
    // =========================
    void AddCube(Vector3 p, Color32 c, int i)
    {
        float s = voxelSize * 0.5f;

        verts.Add(p + new Vector3(-s, -s, -s));
        verts.Add(p + new Vector3( s, -s, -s));
        verts.Add(p + new Vector3( s,  s, -s));
        verts.Add(p + new Vector3(-s,  s, -s));
        verts.Add(p + new Vector3(-s, -s,  s));
        verts.Add(p + new Vector3( s, -s,  s));
        verts.Add(p + new Vector3( s,  s,  s));
        verts.Add(p + new Vector3(-s,  s,  s));

        for (int j = 0; j < 8; j++)
            cols.Add(c);

        // 6 caras x 2 triángulos x 3 vértices = 36 índices
        int[] t = { 0,2,1, 0,3,2, 1,2,6, 6,5,1, 4,5,6, 6,7,4,
                    2,3,7, 7,6,2, 0,7,3, 0,4,7, 0,1,5, 0,5,4 };
        foreach (var tri in t)
            tris.Add(i + tri);
    }
}
