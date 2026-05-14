using UnityEngine;

public class Map3DManager : MonoBehaviour
{
    [Header("3D Map Objects")]
    public GameObject wallMap;
    public GameObject meshMap;
    public GameObject octoMap;
    public GameObject particleMap;

    public GameObject activeMap { get; private set; }

    void Start()
    {
        // Estado inicial
        ShowWallMap();
    }

    public void ShowWallMap()
    {
        activeMap = wallMap;
        SetActiveMap(wallMap);
        Debug.Log("Map3DManager: WallMap activado");
    }

    public void ShowMeshMap()
    {
        activeMap = meshMap;
        SetActiveMap(meshMap);
        Debug.Log("Map3DManager: MeshMap activado");
    }

    public void ShowOctoMap()
    {
        activeMap = octoMap;
        SetActiveMap(octoMap);
        Debug.Log("Map3DManager: OctoMap activado");
    }

    public void ShowParticleMap()
    {
        activeMap = particleMap;
        SetActiveMap(particleMap);
        Debug.Log("Map3DManager: ParticleMap activado");
    }

    private void SetActiveMap(GameObject active)
    {
        if (wallMap != null)
            wallMap.SetActive(active == wallMap);

        if (meshMap != null)
            meshMap.SetActive(active == meshMap);

        if (octoMap != null)
            octoMap.SetActive(active == octoMap);

        if (particleMap != null)
            particleMap.SetActive(active == particleMap);
    }
}