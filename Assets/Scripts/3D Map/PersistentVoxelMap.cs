using System.Collections.Generic;
using UnityEngine;

public class PersistentVoxelMap : MonoBehaviour
{
    public float voxelSize = 0.1f;

    public struct VoxelPoint
    {
        public Vector3 pos;
        public Color32 col;
    }

    private Dictionary<Vector3Int, VoxelPoint> voxels =
        new Dictionary<Vector3Int, VoxelPoint>();

    private bool dirty = false;

    Vector3Int WorldToVoxel(Vector3 p)
    {
        return new Vector3Int(
            Mathf.FloorToInt(p.x / voxelSize),
            Mathf.FloorToInt(p.y / voxelSize),
            Mathf.FloorToInt(p.z / voxelSize)
        );
    }

    public void AddPoint(Vector3 pos, Color32 col)
    {
        var key = WorldToVoxel(pos);

        if (!voxels.ContainsKey(key))
        {
            voxels[key] = new VoxelPoint
            {
                pos = new Vector3(
                    key.x * voxelSize,
                    key.y * voxelSize,
                    key.z * voxelSize
                ),
                col = col
            };
            dirty = true;
        }
    }

    public bool HasNewData() => dirty;

    public void ClearDirtyFlag() => dirty = false;

    public VoxelPoint[] GetAllPoints()
    {
        var arr = new VoxelPoint[voxels.Count];
        int i = 0;
        foreach (var v in voxels.Values)
            arr[i++] = v;
        return arr;
    }
}
