//using System.Collections.Generic;
//using UnityEngine;

//public class PersistentVoxelMap : MonoBehaviour
//{
//    public float voxelSize = 0.1f;
//    public bool generateColliders = true;
//    public int collidersPerFrame = 50;

//    private readonly object voxelLock = new object();

//    public struct VoxelPoint
//    {
//        public Vector3 pos;
//        public Color32 col;
//        public bool hasCollider;
//    }

//    private Dictionary<Vector3Int, VoxelPoint> voxels =
//        new Dictionary<Vector3Int, VoxelPoint>();

//    private List<VoxelPoint> cachedPoints = new List<VoxelPoint>();
//    private bool cacheDirty = true;

//    private Queue<Vector3Int> pendingColliders =
//        new Queue<Vector3Int>();

//    // =========================================================
//    // ADD POINT (THREAD SAFE)
//    // =========================================================
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

//            VoxelPoint v = new VoxelPoint
//            {
//                pos = new Vector3(
//                    (key.x + 0.5f) * voxelSize,
//                    (key.y + 0.5f) * voxelSize,
//                    (key.z + 0.5f) * voxelSize),
//                col = col,
//                hasCollider = false
//            };

//            voxels[key] = v;

//            if (generateColliders)
//                pendingColliders.Enqueue(key);

//            cacheDirty = true;
//        }
//    }

//    // =========================================================
//    // GET POINTS (THREAD SAFE + SIN CRASH)
//    // =========================================================
//    public List<VoxelPoint> GetPoints()
//    {
//        lock (voxelLock)
//        {
//            if (!cacheDirty)
//                return cachedPoints;

//            cachedPoints.Clear();

//            foreach (var v in voxels.Values)
//                cachedPoints.Add(v);

//            cacheDirty = false;
//            return cachedPoints;
//        }
//    }

//    public bool HasData()
//    {
//        lock (voxelLock)
//        {
//            return voxels.Count > 0;
//        }
//    }

//    // =========================================================
//    // COLLIDERS (THREAD SAFE)
//    // =========================================================
//    void Update()
//    {
//        int created = 0;

//        lock (voxelLock)
//        {
//            while (pendingColliders.Count > 0 && created < collidersPerFrame)
//            {
//                var key = pendingColliders.Dequeue();

//                if (!voxels.TryGetValue(key, out VoxelPoint v))
//                    continue;

//                if (v.hasCollider)
//                    continue;

//                GameObject go = new GameObject("VoxelCollider");
//                go.transform.SetParent(transform);
//                go.transform.position = v.pos;

//                BoxCollider bc = go.AddComponent<BoxCollider>();
//                bc.size = Vector3.one * voxelSize;

//                v.hasCollider = true;
//                voxels[key] = v;

//                created++;
//            }
//        }
//    }

//    // =========================================================
//    // CLEAR
//    // =========================================================
//    public void ClearAll()
//    {
//        lock (voxelLock)
//        {
//            voxels.Clear();
//            cachedPoints.Clear();
//            cacheDirty = true;
//        }

//        foreach (Transform child in transform)
//            Destroy(child.gameObject);
//    }
//}










//using System.Collections.Generic;
//using UnityEngine;

//[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
//public class PersistentVoxelMap : MonoBehaviour
//{
//    [Header("Voxel")]
//    public float voxelSize = 0.1f;

//    [Header("Mesh")]
//    public bool generateMesh = true;
//    public float meshUpdateInterval = 1.0f;

//    [Header("Material")]
//    public Material voxelMaterial; // ASIGNAR EN INSPECTOR

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

//    void Awake()
//    {
//        mesh = new Mesh();
//        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

//        GetComponent<MeshFilter>().mesh = mesh;

//        // MATERIAL
//        var mr = GetComponent<MeshRenderer>();
//        if (voxelMaterial != null)
//            mr.material = voxelMaterial;
//        else
//            Debug.LogWarning(" Asigna voxelMaterial para evitar ROSA");

//        // COLLIDER
//        meshCollider = gameObject.GetComponent<MeshCollider>();
//        if (meshCollider == null)
//            meshCollider = gameObject.AddComponent<MeshCollider>();
//    }

//    // =========================
//    // ADD POINT
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

//        //  MUY IMPORTANTE
//        mesh.RecalculateBounds();

//        // COLLIDER
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