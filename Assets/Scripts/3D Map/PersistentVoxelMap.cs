using System.Collections.Generic;
using UnityEngine;

public class PersistentVoxelMap : MonoBehaviour
{
    [Header("Voxel settings")]
    public float voxelSize = 0.1f;

    [Header("Collider settings")]
    public bool generateColliders = true;
    public int collidersPerFrame = 20;

    private readonly object voxelLock = new object();

    public struct VoxelPoint
    {
        public Vector3 pos;
        public Color32 col;
        public bool hasCollider;
    }

    private Dictionary<Vector3Int, VoxelPoint> voxels =
        new Dictionary<Vector3Int, VoxelPoint>();

    private Queue<Vector3Int> pendingColliderKeys =
        new Queue<Vector3Int>();

    private bool dirty = false;

    private Transform colliderRoot;

    void Awake()
    {
        colliderRoot = new GameObject("VoxelColliders").transform;
        colliderRoot.SetParent(transform);
        colliderRoot.gameObject.layer = LayerMask.NameToLayer("Environment");
    }

    // VOXEL UTILS
    Vector3Int WorldToVoxel(Vector3 p)
    {
        return new Vector3Int(
            Mathf.FloorToInt(p.x / voxelSize),
            Mathf.FloorToInt(p.y / voxelSize),
            Mathf.FloorToInt(p.z / voxelSize)
        );
    }

    Vector3 VoxelToWorld(Vector3Int v)
    {
        return new Vector3(
            (v.x + 0.5f) * voxelSize,
            (v.y + 0.5f) * voxelSize,
            (v.z + 0.5f) * voxelSize
        );
    }

    public void AddPoint(Vector3 pos, Color32 col)
    {
        var key = WorldToVoxel(pos);

        lock (voxelLock)
        {
            if (voxels.ContainsKey(key))
                return;

            voxels[key] = new VoxelPoint
            {
                pos = VoxelToWorld(key),
                col = col,
                hasCollider = false
            };

            if (generateColliders)
                pendingColliderKeys.Enqueue(key);

            dirty = true;
        }
    }

    public bool HasNewData() => dirty;

    public void ClearDirtyFlag() => dirty = false;

    public VoxelPoint[] GetAllPoints()
    {
        lock (voxelLock)
        {
            var arr = new VoxelPoint[voxels.Count];
            int i = 0;
            foreach (var v in voxels.Values)
                arr[i++] = v;
            return arr;
        }
    }

    // COLLIDER GENERATION
    void Update()
    {
        if (!generateColliders)
            return;

        int created = 0;

        lock (voxelLock)
        {
            while (pendingColliderKeys.Count > 0 && created < collidersPerFrame)
            {
                var key = pendingColliderKeys.Dequeue();

                if (!voxels.TryGetValue(key, out VoxelPoint voxel))
                    continue;

                if (voxel.hasCollider)
                    continue;

                CreateCollider(voxel);
                voxel.hasCollider = true;
                voxels[key] = voxel;

                created++;
            }
        }
    }
    void CreateCollider(VoxelPoint voxel)
    {
        GameObject go = new GameObject("VoxelCollider");
        go.transform.SetParent(colliderRoot);
        go.transform.position = voxel.pos;
        go.transform.rotation = Quaternion.identity;
        go.isStatic = true;

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.size = Vector3.one * voxelSize;
        bc.center = Vector3.zero;
    }
}
