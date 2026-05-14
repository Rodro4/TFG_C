//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Sensor;

//[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
//public class MeshMap : MonoBehaviour
//{
//    [Header("Voxel")]
//    public float voxelSize = 0.1f;

//    [Header("Mesh")]
//    public bool generateMesh = true;
//    public float meshUpdateInterval = 1.0f;

//    [Header("Material")]
//    public Material voxelMaterial;

//    [Header("ROS")]
//    public string topic = "/camera/depth/points";
//    public bool useROS = true;
//    public int skip = 10;

//    private readonly object voxelLock = new object();

//    public struct VoxelPoint
//    {
//        public Vector3 pos;
//        public Color32 col;
//    }

//    private Dictionary<Vector3Int, VoxelPoint> voxels = new();
//    private List<VoxelPoint> cached = new();

//    private bool dirty = false;
//    private float timer = 0f;

//    private Mesh mesh;
//    private MeshCollider meshCollider;

//    private List<Vector3> verts = new();
//    private List<Color32> cols = new();
//    private List<int> tris = new();

//    // ROS
//    private RosSocket socket;
//    private TfManager tf;

//    void Awake()
//    {
//        mesh = new Mesh();
//        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

//        GetComponent<MeshFilter>().mesh = mesh;

//        var mr = GetComponent<MeshRenderer>();
//        if (voxelMaterial != null)
//            mr.material = voxelMaterial;
//        else
//            Debug.LogWarning("Asigna voxelMaterial para evitar ROSA");

//        meshCollider = gameObject.GetComponent<MeshCollider>();
//        if (meshCollider == null)
//            meshCollider = gameObject.AddComponent<MeshCollider>();
//    }

//    void Start()
//    {
//        if (useROS)
//        {
//            var conn = FindObjectOfType<RosConnector>();
//            if (conn == null) return;

//            socket = conn.RosSocket;
//            tf = FindObjectOfType<TfManager>();

//            socket.Subscribe<PointCloud2>(topic, Receive, 0);
//        }
//    }

//    // =========================
//    // ROS RECEIVE
//    // =========================
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

//            Vector3 p = ros;

//            if (tf != null)
//            {
//                if (!tf.TryTransformPoint(ros, msg.header.frame_id, "map", out p))
//                    continue;
//            }

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

//            AddPoint(p, c);
//        }
//    }

//    // =========================
//    // VOXELS
//    // =========================
//    public void AddPoint(Vector3 pos, Color32 col)
//    {
//        Vector3Int key = new Vector3Int(
//            Mathf.FloorToInt(pos.x / voxelSize),
//            Mathf.FloorToInt(pos.y / voxelSize),
//            Mathf.FloorToInt(pos.z / voxelSize)
//        );

//        lock (voxelLock)
//        {
//            if (voxels.ContainsKey(key))
//                return;

//            voxels[key] = new VoxelPoint
//            {
//                pos = new Vector3(
//                    (key.x + 0.5f) * voxelSize,
//                    (key.y + 0.5f) * voxelSize,
//                    (key.z + 0.5f) * voxelSize),
//                col = col
//            };

//            dirty = true;
//        }
//    }

//    public bool HasData()
//    {
//        lock (voxelLock)
//            return voxels.Count > 0;
//    }

//    public List<VoxelPoint> GetPoints()
//    {
//        lock (voxelLock)
//        {
//            cached.Clear();
//            cached.Capacity = voxels.Count;

//            foreach (var v in voxels.Values)
//                cached.Add(v);

//            return cached;
//        }
//    }

//    public void ClearAll()
//    {
//        lock (voxelLock)
//        {
//            voxels.Clear();
//            cached.Clear();
//        }

//        mesh.Clear();

//        if (meshCollider != null)
//            meshCollider.sharedMesh = null;
//    }

//    // =========================
//    // UPDATE
//    // =========================
//    void Update()
//    {
//        if (!generateMesh || !dirty)
//            return;

//        timer += Time.deltaTime;
//        if (timer < meshUpdateInterval)
//            return;

//        timer = 0;
//        dirty = false;

//        RebuildMesh();
//    }

//    // =========================
//    // MESH BUILD
//    // =========================
//    void RebuildMesh()
//    {
//        var pts = GetPoints();

//        verts.Clear();
//        cols.Clear();
//        tris.Clear();

//        int index = 0;

//        foreach (var v in pts)
//        {
//            AddCube(v.pos, v.col, index);
//            index += 8;
//        }

//        mesh.Clear();
//        mesh.SetVertices(verts);
//        mesh.SetColors(cols);
//        mesh.SetTriangles(tris, 0);
//        mesh.RecalculateNormals();
//        mesh.RecalculateBounds();

//        meshCollider.sharedMesh = null;
//        meshCollider.sharedMesh = mesh;
//    }

//    // =========================
//    // CUBE
//    // =========================
//    void AddCube(Vector3 p, Color32 c, int i)
//    {
//        float s = voxelSize * 0.5f;

//        Vector3[] v =
//        {
//            p + new Vector3(-s,-s,-s),
//            p + new Vector3(s,-s,-s),
//            p + new Vector3(s,s,-s),
//            p + new Vector3(-s,s,-s),
//            p + new Vector3(-s,-s,s),
//            p + new Vector3(s,-s,s),
//            p + new Vector3(s,s,s),
//            p + new Vector3(-s,s,s)
//        };

//        verts.AddRange(v);

//        for (int j = 0; j < 8; j++)
//            cols.Add(c);

//        int[] t =
//        {
//            0,2,1, 0,3,2,
//            1,2,6, 6,5,1,
//            4,5,6, 6,7,4,
//            2,3,7, 7,6,2,
//            0,7,3, 0,4,7,
//            0,1,5, 0,5,4
//        };

//        foreach (var tri in t)
//            tris.Add(i + tri);
//    }
//}



































using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshMap : MonoBehaviour
{
    [Header("Voxel")]
    public float voxelSize = 0.1f;

    [Header("Mesh")]
    public bool generateMesh = true;
    public float meshUpdateInterval = 1.0f;

    [Header("Material")]
    public Material voxelMaterial;

    [Header("ROS")]
    public string topic = "/camera/depth/points";
    public bool useROS = true;
    public int skip = 10;

    private readonly object voxelLock = new object();

    //robotmotiondetector
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

    private Mesh mesh;
    private MeshCollider meshCollider;

    private List<Vector3> verts = new();
    private List<Color32> cols = new();
    private List<int> tris = new();

    // ROS
    private RosSocket socket;
    private TfManager tf;

    void Awake()
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        GetComponent<MeshFilter>().mesh = mesh;

        var mr = GetComponent<MeshRenderer>();
        if (voxelMaterial != null)
            mr.material = voxelMaterial;
        else
            Debug.LogWarning("Asigna voxelMaterial para evitar ROSA");

        meshCollider = gameObject.GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();
    }

    void Start()
    {
        if (useROS)
        {
            var conn = FindObjectOfType<RosConnector>();
            if (conn == null) return;

            socket = conn.RosSocket;
            tf = FindObjectOfType<TfManager>();

            socket.Subscribe<PointCloud2>(topic, Receive, 0);
        }
    }

    // =========================
    // ROS RECEIVE
    // =========================
    void Receive(PointCloud2 msg)
    {
        var mapManager = FindObjectOfType<Map3DManager>();
        if (mapManager.activeMap != gameObject)
            return;  // Ignorar si este mapa no está activo

        if (!useROS)
            return;

        //robotmotiondetector
        if (motionDetector != null && !motionDetector.isStationary)
        {
            return; // IGNORAR NUBE SI SE MUEVE
        }

        int step = (int)msg.point_step;
        int count = (int)(msg.width * msg.height);

        int ox = -1, oy = -1, oz = -1, oc = -1;

        foreach (var f in msg.fields)
        {
            if (f.name == "x") ox = (int)f.offset;
            if (f.name == "y") oy = (int)f.offset;
            if (f.name == "z") oz = (int)f.offset;
            if (f.name == "rgb") oc = (int)f.offset;
        }

        for (int i = 0; i < count; i += skip)
        {
            int idx = i * step;

            float x = BitConverter.ToSingle(msg.data, idx + ox);
            float y = BitConverter.ToSingle(msg.data, idx + oy);
            float z = BitConverter.ToSingle(msg.data, idx + oz);

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;

            Vector3 ros = new Vector3(-y, z, x);

            Vector3 p = ros;

            double cloudTime =
            msg.header.stamp.secs +
            msg.header.stamp.nsecs * 1e-9;

            if (!tf.TryTransformPointAtTime(
                ros,
                msg.header.frame_id,
                "map",
                cloudTime,
                out p))
            {
                continue;
            }

            Color32 c = Color.white;

            if (oc != -1)
            {
                uint rgb = BitConverter.ToUInt32(msg.data, idx + oc);
                c = new Color32(
                    (byte)((rgb >> 16) & 255),
                    (byte)((rgb >> 8) & 255),
                    (byte)(rgb & 255),
                    255
                );
            }

            AddPoint(p, c);
        }
    }

    // =========================
    // VOXELS
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
        lock (voxelLock)
            return voxels.Count > 0;
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
    // UPDATE
    // =========================
    void Update()
    {
        if (!generateMesh || !dirty)
            return;

        timer += Time.deltaTime;
        if (timer < meshUpdateInterval)
            return;

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

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    // =========================
    // CUBE
    // =========================
    void AddCube(Vector3 p, Color32 c, int i)
    {
        float s = voxelSize * 0.5f;

        Vector3[] v =
        {
            p + new Vector3(-s,-s,-s),
            p + new Vector3(s,-s,-s),
            p + new Vector3(s,s,-s),
            p + new Vector3(-s,s,-s),
            p + new Vector3(-s,-s,s),
            p + new Vector3(s,-s,s),
            p + new Vector3(s,s,s),
            p + new Vector3(-s,s,s)
        };

        verts.AddRange(v);

        for (int j = 0; j < 8; j++)
            cols.Add(c);

        int[] t =
        {
            0,2,1, 0,3,2,
            1,2,6, 6,5,1,
            4,5,6, 6,7,4,
            2,3,7, 7,6,2,
            0,7,3, 0,4,7,
            0,1,5, 0,5,4
        };

        foreach (var tri in t)
            tris.Add(i + tri);
    }
}