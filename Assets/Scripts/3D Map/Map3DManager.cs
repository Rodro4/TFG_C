using UnityEngine;

public class Map3DManager : MonoBehaviour
{
    [Header("3D Map Objects")]
    public GameObject pointCloudMap;
    public GameObject octoMap;
    public GameObject occupancyMap;

    void Start()
    {
        // Estado inicial
        ShowOccupancyMap();
    }

    public void ShowPointCloud()
    {
        SetActiveMap(pointCloudMap);
        Debug.Log("Map3DManager: PointCloud activado");
    }

    public void ShowOctoMap()
    {
        SetActiveMap(octoMap);
        Debug.Log("Map3DManager: OctoMap activado");
    }

    public void ShowOccupancyMap()
    {
        SetActiveMap(occupancyMap);
        Debug.Log("Map3DManager: OccupancyMap activado");
    }

    private void SetActiveMap(GameObject active)
    {
        if (pointCloudMap != null) pointCloudMap.SetActive(active == pointCloudMap);
        if (octoMap != null) octoMap.SetActive(active == octoMap);
        if (occupancyMap != null) occupancyMap.SetActive(active == occupancyMap);
    }
}
