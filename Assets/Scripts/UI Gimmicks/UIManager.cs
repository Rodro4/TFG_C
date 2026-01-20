using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    // --------------------
    // CAMERA / SCREEN UI
    // --------------------

    private VREyeFeed eyeFeed;

    [Header("Display Screens")]
    public GameObject rgbScreen;
    public GameObject depthScreen;

    private bool showingRGB = true;

    // --------------------
    // 3D MAP UI
    // --------------------

    [Header("3D Maps")]
    [Tooltip("PointCloud + PersistentVoxelMap")]
    public GameObject[] pointCloudObjects;

    public GameObject octoMap;
    public GameObject occupancyMap;

    [Header("UI Elements")]
    public TMP_Dropdown mapDropdown;

    void Start()
    {
        eyeFeed = FindObjectOfType<VREyeFeed>();
        if (eyeFeed == null)
            Debug.LogWarning("UIManager: VREyeFeed component not found.");

        SetScreenState(true);
        SetActiveMap(0);

        if (mapDropdown != null)
            mapDropdown.onValueChanged.AddListener(SetActiveMap);
    }

    // --------------------
    // RGB / DEPTH
    // --------------------

    public void ToggleScreen()
    {
        showingRGB = !showingRGB;
        SetScreenState(showingRGB);

        if (eyeFeed != null)
            eyeFeed.SetScreenSource(showingRGB);
    }

    private void SetScreenState(bool rgb)
    {
        if (rgbScreen != null) rgbScreen.SetActive(rgb);
        if (depthScreen != null) depthScreen.SetActive(!rgb);

        Debug.Log("UIManager: Current screen -> " + (rgb ? "RGB" : "Depth"));
    }

    // --------------------
    // 3D MAP SELECTION
    // --------------------

    public void SetActiveMap(int index)
    {
        // PointCloud (varios objetos)
        SetGroupActive(pointCloudObjects, index == 0);

        // OctoMap
        if (octoMap != null)
            octoMap.SetActive(index == 1);

        // Occupancy
        if (occupancyMap != null)
            occupancyMap.SetActive(index == 2);

        Debug.Log($"UIManager: Mapa 3D activo -> {GetMapName(index)}");
    }

    private void SetGroupActive(GameObject[] group, bool active)
    {
        if (group == null) return;

        foreach (var go in group)
        {
            if (go != null)
                go.SetActive(active);
        }
    }

    private string GetMapName(int index)
    {
        return index switch
        {
            0 => "PointCloud (+ PersistentVoxelMap)",
            1 => "OctoMap",
            2 => "OccupancyMap",
            _ => "Unknown"
        };
    }
}
