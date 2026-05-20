using System;
using System.Collections.Generic;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class OctoMapPointCloudSubscriber : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────────

    [Header("ROS")]
    [Tooltip("Desactiva para cargar un mapa guardado sin conectar a ROS.")]
    public bool useROS = true;
    public string pointCloudTopic = "/octomap_point_cloud_centers";

    [Header("Mapa")]
    [Tooltip("Máximo número de voxels acumulados en memoria.")]
    public int maxVoxels = 200000;

    [Tooltip("Saltar 1 de cada N puntos del mensaje. 1 = todos.")]
    public int pointSkip = 1;

    [Header("Visual")]
    [Tooltip("Debe coincidir con _resolution del octomap_server.")]
    public float voxelSize = 0.05f;

    [Tooltip("Material con 'Enable GPU Instancing' activado (Inspector → Material → Enable Instancing).")]
    public Material voxelMaterial;

    [Header("Color de fallback (si el mensaje no trae RGB)")]
    public bool useHeightColor = true;
    public float heightMin = 0f;
    public float heightMax = 1.5f;
    public Gradient heightGradient;

    [Header("Timing")]
    [Tooltip("Segundos entre aplicaciones de datos nuevos al mapa.")]
    public float updateInterval = 0.2f;

    // ─── Privados ────────────────────────────────────────────────────────

    // Mapa acumulativo: clave = posición cuantizada, valor = color
    private Dictionary<Vector3Int, Color32> voxelMap =
        new Dictionary<Vector3Int, Color32>();

    // Double-buffer: ROS escribe aquí, Update() lee
    private readonly object dataLock = new object();
    private List<Vector3Int> pendingKeys   = new List<Vector3Int>();
    private List<Color32>    pendingColors = new List<Color32>();
    private bool dataReady = false;

    // Render cache: se reconstruye solo cuando el mapa cambia
    private Matrix4x4[] matrixCache;
    private Vector4[]   colorCache;
    private int         cachedCount = 0;
    private bool        renderCacheDirty = false;

    // Batch de 1023 elementos (límite de DrawMeshInstanced)
    private readonly Matrix4x4[] batchMatrices = new Matrix4x4[1023];
    private readonly Vector4[]   batchColors   = new Vector4[1023];
    private MaterialPropertyBlock mpb;
    private Mesh cubeMesh;

    // Colliders (post-bake)
    private List<GameObject> colliderObjects = new List<GameObject>();

    private float timer = 0f;

    // ─── Unity lifecycle ─────────────────────────────────────────────────

    void Start()
    {
        cubeMesh = BuildCubeMesh();
        mpb      = new MaterialPropertyBlock();

        // Prealocar cache al tamaño máximo (cero allocations en Update)
        matrixCache = new Matrix4x4[maxVoxels];
        colorCache  = new Vector4[maxVoxels];

        if (voxelMaterial != null)
            voxelMaterial.enableInstancing = true;
        else
            Debug.LogWarning("[OctoMap] Asigna un Material con GPU Instancing activado.");

        if (!useROS)
        {
            Debug.Log("[OctoMap] useROS=false: modo offline, carga un mapa guardado.");
            return;
        }

        var connector = FindObjectOfType<RosConnector>();
        if (connector == null)
        {
            Debug.LogError("[OctoMap] RosConnector no encontrado.");
            return;
        }

        connector.RosSocket.Subscribe<PointCloud2>(pointCloudTopic, OnPointCloudReceived, 0);
        Debug.Log("[OctoMap] Suscrito a " + pointCloudTopic);
    }

    void Update()
    {
        // Aplicar datos nuevos cada updateInterval segundos (solo si ROS activo)
        if (useROS)
        {
            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer = 0f;
                if (dataReady)
                    ApplyPendingData();
            }
        }

        // Reconstruir cache solo cuando el mapa cambió
        if (renderCacheDirty)
            RebuildRenderCache();

        // Dibujar cada frame (GPU instancing lo requiere)
        DrawInstanced();
    }

    void OnDestroy() => ClearMap();

    // ─── ROS callback (hilo secundario de rosbridge) ──────────────────────

    private void OnPointCloudReceived(PointCloud2 msg)
    {
        int step  = (int)msg.point_step;
        int total = (int)(msg.width * msg.height);

        int xOff = -1, yOff = -1, zOff = -1, rgbOff = -1;
        foreach (var f in msg.fields)
        {
            switch (f.name)
            {
                case "x":    xOff   = (int)f.offset; break;
                case "y":    yOff   = (int)f.offset; break;
                case "z":    zOff   = (int)f.offset; break;
                case "rgb":
                case "rgba": rgbOff = (int)f.offset; break;
            }
        }
        if (xOff < 0 || yOff < 0 || zOff < 0) return;

        bool hasColor = rgbOff >= 0;

        var localKeys   = new List<Vector3Int>(2048);
        var localColors = new List<Color32>(2048);

        for (int i = 0; i < total; i += pointSkip)
        {
            int idx = i * step;

            float rx = BitConverter.ToSingle(msg.data, idx + xOff);
            float ry = BitConverter.ToSingle(msg.data, idx + yOff);
            float rz = BitConverter.ToSingle(msg.data, idx + zOff);

            if (float.IsNaN(rx) || float.IsNaN(ry) || float.IsNaN(rz)) continue;

            // ROS (X adelante, Y izquierda, Z arriba) → Unity (X derecha, Y arriba, Z adelante)
            Vector3 unityPos = new Vector3(-ry, rz, rx);

            // FloorToInt: más estable que RoundToInt en rejillas espaciales
            Vector3Int key = new Vector3Int(
                Mathf.FloorToInt(unityPos.x / voxelSize),
                Mathf.FloorToInt(unityPos.y / voxelSize),
                Mathf.FloorToInt(unityPos.z / voxelSize));

            Color32 color;
            if (hasColor)
            {
                // ROS empaqueta RGB little-endian: byte0=B, byte1=G, byte2=R
                byte b = msg.data[idx + rgbOff + 0];
                byte g = msg.data[idx + rgbOff + 1];
                byte r = msg.data[idx + rgbOff + 2];
                color = new Color32(r, g, b, 255);
            }
            else if (useHeightColor)
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(heightMin, heightMax, rz));
                color   = (Color32)heightGradient.Evaluate(t);
                color.a = 255;
            }
            else
            {
                color = Color.white;
            }

            localKeys.Add(key);
            localColors.Add(color);
        }

        lock (dataLock)
        {
            pendingKeys   = localKeys;
            pendingColors = localColors;
            dataReady     = true;
        }
    }

    // ─── Aplicar datos al mapa (hilo principal) ───────────────────────────

    private void ApplyPendingData()
    {
        List<Vector3Int> keys;
        List<Color32>    cols;

        lock (dataLock)
        {
            keys      = pendingKeys;
            cols      = pendingColors;
            dataReady = false;
        }

        bool changed = false;

        for (int i = 0; i < keys.Count; i++)
        {
            if (voxelMap.Count >= maxVoxels && !voxelMap.ContainsKey(keys[i]))
                break;

            voxelMap[keys[i]] = cols[i];
            changed = true;
        }

        if (changed)
            renderCacheDirty = true;
    }

    // ─── Reconstruir cache (solo cuando renderCacheDirty == true) ─────────

    private void RebuildRenderCache()
    {
        int n = 0;

        foreach (var kv in voxelMap)
        {
            if (n >= maxVoxels) break;

            Vector3 pos = new Vector3(
                (kv.Key.x + 0.5f) * voxelSize,
                (kv.Key.y + 0.5f) * voxelSize,
                (kv.Key.z + 0.5f) * voxelSize);

            matrixCache[n] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * voxelSize);

            Color32 c = kv.Value;
            colorCache[n] = new Vector4(c.r / 255f, c.g / 255f, c.b / 255f, 1f);

            n++;
        }

        cachedCount      = n;
        renderCacheDirty = false;
    }

    // ─── Dibujar instancias (cada frame) ─────────────────────────────────

    private void DrawInstanced()
    {
        if (cachedCount == 0 || cubeMesh == null || voxelMaterial == null) return;

        int drawn = 0;
        while (drawn < cachedCount)
        {
            int batchSize = Mathf.Min(1023, cachedCount - drawn);

            Array.Copy(matrixCache, drawn, batchMatrices, 0, batchSize);
            Array.Copy(colorCache,  drawn, batchColors,   0, batchSize);

            mpb.SetVectorArray("_Color", batchColors);
            Graphics.DrawMeshInstanced(cubeMesh, 0, voxelMaterial, batchMatrices, batchSize, mpb);

            drawn += batchSize;
        }
    }

    // ─── Colliders (post-bake) ────────────────────────────────────────────

    /// <summary>
    /// Genera BoxColliders para todos los voxels actuales.
    /// Usar cuando el mapa esté completo, no en tiempo real.
    /// Accesible desde clic derecho en el componente (Context Menu).
    /// </summary>
    [ContextMenu("Generar Colliders")]
    public void BakeColliders()
    {
        foreach (var go in colliderObjects)
            if (go != null) Destroy(go);
        colliderObjects.Clear();

        if (voxelMap.Count == 0)
        {
            Debug.LogWarning("[OctoMap] BakeColliders: el mapa está vacío.");
            return;
        }

        GameObject parent = new GameObject("OctoMap_Colliders");
        parent.transform.SetParent(transform);

        foreach (var kv in voxelMap)
        {
            Vector3 pos = new Vector3(
                (kv.Key.x + 0.5f) * voxelSize,
                (kv.Key.y + 0.5f) * voxelSize,
                (kv.Key.z + 0.5f) * voxelSize);

            GameObject go = new GameObject("Col");
            go.transform.SetParent(parent.transform);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().size = Vector3.one * voxelSize;
            colliderObjects.Add(go);
        }

        Debug.Log($"[OctoMap] BakeColliders: {colliderObjects.Count} colliders generados.");
    }

    // ─── API pública ──────────────────────────────────────────────────────

    public int VoxelCount => voxelMap.Count;

    [ContextMenu("Limpiar Mapa")]
    public void ClearMap()
    {
        voxelMap.Clear();
        cachedCount      = 0;
        renderCacheDirty = false;

        foreach (var go in colliderObjects)
            if (go != null) Destroy(go);
        colliderObjects.Clear();

        Debug.Log("[OctoMap] Mapa limpiado.");
    }

    // ─── Save / Load (usado por MapSaverLoaderManager) ───────────────────
    //
    // IMPORTANTE: se guardan posiciones de mundo (float), NO las claves
    // cuantizadas, para que el mapa sea independiente de voxelSize.
    // Al cargar se re-cuantiza con el voxelSize actual del componente.

    public (Vector3[] pos, Color32[] col, int count) GetCurrentPoints()
    {
        int n   = voxelMap.Count;
        var pos = new Vector3[n];
        var col = new Color32[n];
        int i   = 0;

        foreach (var kv in voxelMap)
        {
            // Centro geométrico del voxel en espacio mundo
            pos[i] = new Vector3(
                (kv.Key.x + 0.5f) * voxelSize,
                (kv.Key.y + 0.5f) * voxelSize,
                (kv.Key.z + 0.5f) * voxelSize);
            col[i] = kv.Value;
            i++;
        }

        return (pos, col, n);
    }

    public void LoadPoints(Vector3[] pos, Color32[] col, int count)
    {
        voxelMap.Clear();
        int n = Mathf.Min(count, maxVoxels);

        for (int i = 0; i < n; i++)
        {
            // Re-cuantizar con el voxelSize actual
            // (el centro guardado - 0.5*voxelSize queda en el origen del voxel,
            //  FloorToInt lo redondea a la celda correcta)
            var key = new Vector3Int(
                Mathf.FloorToInt(pos[i].x / voxelSize),
                Mathf.FloorToInt(pos[i].y / voxelSize),
                Mathf.FloorToInt(pos[i].z / voxelSize));
            voxelMap[key] = col[i];
        }

        renderCacheDirty = true;
        Debug.Log($"[OctoMap] {voxelMap.Count} puntos cargados.");
    }

    // ─── Utilidades ───────────────────────────────────────────────────────

    private static Mesh BuildCubeMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh m = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return m;
    }
}