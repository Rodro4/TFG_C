using UnityEngine;
using RosSharp.RosBridgeClient;

public class MapGameHelper : MonoBehaviour
{
    public OccupancyGridSubscriber occupancy;

    public float spawnHeight = 0.2f;
    public float minDistanceBetweenTargets = 2f;

    public Vector3 GetRandomFreeWorldPosition(float minDistanceFrom)
    {
        if (occupancy == null) return Vector3.zero;

        for (int i = 0; i < 500; i++)
        {
            int randomX = Random.Range(0, occupancy.MapWidth);
            int randomY = Random.Range(0, occupancy.MapHeight);

            Vector2Int cell = new Vector2Int(randomX, randomY);
            int value = occupancy.GetMapCell(cell);

            if (value != 0) continue; // Solo celdas libres

            Vector3 worldPos = MapToWorld(cell);

            if (Vector3.Distance(worldPos, Vector3.zero) > minDistanceFrom)
            {
                return worldPos;
            }
        }

        return Vector3.zero;
    }

    public Vector3 MapToWorld(Vector2Int cell)
    {
        float x = cell.x * occupancy.MapResolution + occupancy.MapOrigin.x;
        float z = cell.y * occupancy.MapResolution + occupancy.MapOrigin.z;

        return new Vector3(x, spawnHeight, z);
    }
}
