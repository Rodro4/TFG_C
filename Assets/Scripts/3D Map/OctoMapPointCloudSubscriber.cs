using System;
using System.Collections.Generic;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class OctoMapPointCloudSubscriber : MonoBehaviour
{
    // =====================================================================
    // INSPECTOR
    // =====================================================================

    [Header("ROS")]
    public string PointCloudTopic = "/octomap_point_cloud_centers";
    public bool useROS = true;

    [Header("Mapa")]
    [Tooltip("Máximo número de voxels acumulados en memoria.")]
    public int MaxPoints = 300000;

    [Tooltip("Usar 1 de cada N puntos del mensaje (1 = todos, 2 = la mitad...).")]
    public int PointSkip = 1;

    [Header("Visual")]
    [Tooltip("Debe coincidir con _resolution del octomap_server (por defecto 0.05).")]
    public float voxelSize = 0.05f;

    [Tooltip("Multiplicador sobre voxelSize para el tamaño visual. 1.1 cierra huecos entre voxels.")]
    [Range(0.5f, 2f)]
    public float particleSizeMultiplier = 1.1f;

    [Header("Modo de render")]
    [Tooltip("True = cubos instanciados en GPU (más sólido, sin huecos). False = Particle System.")]
    public bool useCubeMesh = true;

    [Tooltip("Mesh del cubo. Se genera automáticamente si se deja vacío.")]
    public Mesh cubeMesh;

    [Tooltip("Material con GPU Instancing activado. Ver instrucciones en el script.")]
    public Material cubeMaterial;

    [Header("Timing")]
    [Tooltip("Intervalo en segundos entre actualizaciones del render. 0.1 es un buen balance.")]
    public float UpdateInterval = 0.1f;

    [Header("Color de fallback (si el mensaje no trae campo RGB)")]
    public bool useHeightColorFallback = true;
    public float alturaMin = 0f;
    public float alturaMax = 1.5f;
    public Gradient miGradiente;

    [Header("Colliders")]
    [Tooltip("Genera BoxColliders por voxel. Muy costoso con muchos puntos.")]
    public bool generateColliders = false;
    [Tooltip("Colliders creados por frame para repartir la carga.")]
    public int collidersPerFrame = 10;

    // =====================================================================
    // PRIVADOS
    // =====================================================================

    private RosSocket rosSocket;

    // Mapa acumulativo sin duplicados.
    // Clave: posición cuantizada con FloorToInt a la rejilla del octomap.
    // Valor: color RGB del voxel.
    private Dictionary<Vector3Int, Color32> voxelMap =
        new Dictionary<Vector3Int, Color32>(300000);

    // Double-buffer: el hilo de ROS escribe aquí, el hilo principal lee.
    private readonly object dataLock = new object();
    private List<Vector3Int> pendingKeys   = new List<Vector3Int>(4096);
    private List<Color32>    pendingColors = new List<Color32>(4096);
    private bool dataReady = false;

    // Particle System
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    // GPU Instanced
    private Matrix4x4[]          instMatrices   = new Matrix4x4[1023];
    private Vector4[]             instColors     = new Vector4[1023];
    private MaterialPropertyBlock mpb;

    // Colliders
    private Queue<Vector3Int> pendingColliderKeys = new Queue<Vector3Int>();
    private List<GameObject>  colliderObjects     = new List<GameObject>();

    private float timer = 0f;

    private bool mapDirty = true;

    // =====================================================================
    // UNITY LIFECYCLE
    // =====================================================================

    void Start()
    {
        InitRendering();

        if (!useROS) return;

        var rosConnector = FindObjectOfType<RosConnector>();
        if (rosConnector == null)
        {
            Debug.LogError("[OctoMap] RosConnector no encontrado en la escena.");
            return;
        }

        rosSocket = rosConnector.RosSocket;
        rosSocket.Subscribe<PointCloud2>(PointCloudTopic, ReceivePointCloud, 0);
        Debug.Log("[OctoMap] Suscrito a " + PointCloudTopic);
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= UpdateInterval)
        {
            timer = 0f;

            if (dataReady)
            {
                ApplyPendingData();
            }
        }

        // GPU instancing necesita draw EVERY FRAME
        if (useCubeMesh)
        {
            RenderInstanced();
        }
        else
        {
            // ParticleSystem sí puede cachearse
            if (mapDirty)
            {
                RenderParticles();
                mapDirty = false;
            }
        }

        if (generateColliders)
            GenerateCollidersBatch();
    }

    void OnDestroy()
    {
        ClearMap();
    }

    // =====================================================================
    // INIT RENDERING
    // =====================================================================

    void InitRendering()
    {
        if (cubeMesh == null)
            cubeMesh = BuildDefaultCubeMesh();

        if (!useCubeMesh)
        {
            // --- Particle System con Mesh cube ---
            ps = GetComponent<ParticleSystem>();
            if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.maxParticles    = MaxPoints;
            main.loop            = false;
            main.playOnAwake     = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime   = float.MaxValue;
            main.startSize       = voxelSize * particleSizeMultiplier;

            var emission = ps.emission;
            emission.enabled = false;

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = cubeMesh;

            if (r.sharedMaterial == null)
                r.sharedMaterial = new Material(Shader.Find("Particles/Standard Unlit"));

            particles = new ParticleSystem.Particle[MaxPoints];
        }
        else
        {
            // --- GPU Instanced ---
            mpb = new MaterialPropertyBlock();

            if (cubeMaterial == null)
            {
                cubeMaterial = new Material(Shader.Find("Standard"));
                cubeMaterial.enableInstancing = true;
                Debug.LogWarning("[OctoMap] cubeMaterial no asignado. " +
                    "El color por voxel requiere el shader Custom/InstancedVoxel. " +
                    "Ver instrucciones en el script.");
            }
            cubeMaterial.enableInstancing = true;
        }
    }

    static Mesh BuildDefaultCubeMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh m = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return m;
    }

    // =====================================================================
    // ROS CALLBACK — hilo secundario de rosbridge
    // =====================================================================

    private void ReceivePointCloud(PointCloud2 msg)
    {
        int pointStep   = (int)msg.point_step;
        int totalPoints = (int)(msg.width * msg.height);

        int xOff = -1, yOff = -1, zOff = -1, rgbOff = -1;
        foreach (var field in msg.fields)
        {
            switch (field.name)
            {
                case "x":    xOff   = (int)field.offset; break;
                case "y":    yOff   = (int)field.offset; break;
                case "z":    zOff   = (int)field.offset; break;
                case "rgb":
                case "rgba": rgbOff = (int)field.offset; break;
            }
        }

        if (xOff == -1 || yOff == -1 || zOff == -1) return;

        bool hasColor = rgbOff != -1;

        var localKeys   = new List<Vector3Int>(4096);
        var localColors = new List<Color32>(4096);

        for (int i = 0; i < totalPoints; i += PointSkip)
        {
            int idx = i * pointStep;

            float rx = BitConverter.ToSingle(msg.data, idx + xOff);
            float ry = BitConverter.ToSingle(msg.data, idx + yOff);
            float rz = BitConverter.ToSingle(msg.data, idx + zOff);

            if (float.IsNaN(rx) || float.IsNaN(ry) || float.IsNaN(rz)) continue;

            // ROS -> Unity:  X->Z,  Y->X,  Z->Y
            Vector3 unityPos = new Vector3(-ry, rz, rx);

            // FloorToInt evita el aliasing de RoundToInt en rejillas espaciales.
            Vector3Int key = new Vector3Int(
                Mathf.FloorToInt(unityPos.x / voxelSize),
                Mathf.FloorToInt(unityPos.y / voxelSize),
                Mathf.FloorToInt(unityPos.z / voxelSize)
            );

            Color32 color;
            if (hasColor)
            {
                // ROS empaqueta el color como 0x00RRGGBB en little-endian:
                // byte[offset+0] = B, byte[offset+1] = G, byte[offset+2] = R
                byte b = msg.data[idx + rgbOff + 0];
                byte g = msg.data[idx + rgbOff + 1];
                byte r = msg.data[idx + rgbOff + 2];
                color = new Color32(r, g, b, 255);
            }
            else if (useHeightColorFallback)
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(alturaMin, alturaMax, rz));
                Color c = miGradiente.Evaluate(t);
                c.a = 1f;
                color = (Color32)c;
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

    // =====================================================================
    // APPLY - hilo principal
    // =====================================================================

    void ApplyPendingData()
    {
        List<Vector3Int> keys;
        List<Color32>    cols;

        lock (dataLock)
        {
            keys      = pendingKeys;
            cols      = pendingColors;
            dataReady = false;
        }

        int count = keys.Count;

        for (int i = 0; i < count; i++)
        {
            if (voxelMap.Count >= MaxPoints && !voxelMap.ContainsKey(keys[i]))
                break; // mapa lleno, no añadir nuevos

            bool isNew = !voxelMap.ContainsKey(keys[i]);

            // Actualizar siempre el color (octomap puede re-colorear voxels ya vistos)
            voxelMap[keys[i]] = cols[i];

            if (isNew && generateColliders)
                pendingColliderKeys.Enqueue(keys[i]);
        }

        mapDirty = true;
    }

    // =====================================================================
    // RENDER - PARTICLE SYSTEM
    // =====================================================================

    void RenderParticles()
    {
        if (ps == null) return;

        float size = voxelSize * particleSizeMultiplier;
        int   n    = 0;

        foreach (var kv in voxelMap)
        {
            if (n >= MaxPoints) break;

            // Centro del voxel: (key + 0.5) * voxelSize
            particles[n].position = new Vector3(
                (kv.Key.x + 0.5f) * voxelSize,
                (kv.Key.y + 0.5f) * voxelSize,
                (kv.Key.z + 0.5f) * voxelSize
            );
            particles[n].startColor        = kv.Value;
            particles[n].startSize         = size;
            particles[n].remainingLifetime = float.MaxValue;
            n++;
        }

        ps.SetParticles(particles, n);
    }

    // =====================================================================
    // RENDER - GPU INSTANCED MESH
    // =====================================================================

    void RenderInstanced()
    {
        if (voxelMap.Count == 0) return;

        if (cubeMesh == null || cubeMaterial == null) return;

        float size  = voxelSize * particleSizeMultiplier;
        int   index = 0;

        foreach (var kv in voxelMap)
        {
            Vector3 pos = new Vector3(
                (kv.Key.x + 0.5f) * voxelSize,
                (kv.Key.y + 0.5f) * voxelSize,
                (kv.Key.z + 0.5f) * voxelSize
            );

            instMatrices[index] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * size);

            Color32 c = kv.Value;
            instColors[index] = new Vector4(c.r / 255f, c.g / 255f, c.b / 255f, 1f);

            index++;

            if (index == 1023)
            {
                FlushBatch(index);
                index = 0;
            }
        }

        if (index > 0)
            FlushBatch(index);
    }

    void FlushBatch(int count)
    {
    //     mpb.SetVectorArray("_Color", instColors);
    //     Graphics.DrawMeshInstanced(cubeMesh, 0, cubeMaterial, instMatrices, count, mpb);
    Vector4[] colorsToSend = new Vector4[count];
    Matrix4x4[] matricesToSend = new Matrix4x4[count];

    Array.Copy(instColors, colorsToSend, count);
    Array.Copy(instMatrices, matricesToSend, count);

    mpb.SetVectorArray("_Color", colorsToSend);
    Graphics.DrawMeshInstanced(cubeMesh, 0, cubeMaterial, matricesToSend, count, mpb);
    }

    // =====================================================================
    // COLLIDERS - progresivo para no bloquear frames
    // =====================================================================

    void GenerateCollidersBatch()
    {
        int created = 0;
        while (pendingColliderKeys.Count > 0 && created < collidersPerFrame)
        {
            Vector3Int key = pendingColliderKeys.Dequeue();

            GameObject go = new GameObject("OctoCollider");
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(
                (key.x + 0.5f) * voxelSize,
                (key.y + 0.5f) * voxelSize,
                (key.z + 0.5f) * voxelSize
            );
            go.AddComponent<BoxCollider>().size = Vector3.one * voxelSize;
            colliderObjects.Add(go);
            created++;
        }
    }

    // =====================================================================
    // API PÚBLICA
    // =====================================================================

    public int GetPointCount() => voxelMap.Count;

    public void ClearMap()
    {
        voxelMap.Clear();
        pendingColliderKeys.Clear();

        if (ps != null)
            ps.SetParticles(new ParticleSystem.Particle[0], 0);

        foreach (var go in colliderObjects)
            if (go != null) Destroy(go);

        colliderObjects.Clear();
        Debug.Log("[OctoMap] Mapa limpiado.");
    }

    // Compatibilidad con MapSaverLoaderManager
    public (Vector3[] pos, Color32[] col, int count) GetCurrentPoints()
    {
        int n   = Mathf.Min(voxelMap.Count, MaxPoints);
        var pos = new Vector3[n];
        var col = new Color32[n];
        int i   = 0;

        foreach (var kv in voxelMap)
        {
            if (i >= n) break;
            pos[i] = new Vector3(
                (kv.Key.x + 0.5f) * voxelSize,
                (kv.Key.y + 0.5f) * voxelSize,
                (kv.Key.z + 0.5f) * voxelSize
            );
            col[i] = kv.Value;
            i++;
        }

        return (pos, col, n);
    }

    public void LoadPoints(Vector3[] pos, Color32[] col, int count)
    {
        voxelMap.Clear();
        int n = Mathf.Min(count, MaxPoints);

        for (int i = 0; i < n; i++)
        {
            Vector3Int key = new Vector3Int(
                Mathf.FloorToInt(pos[i].x / voxelSize),
                Mathf.FloorToInt(pos[i].y / voxelSize),
                Mathf.FloorToInt(pos[i].z / voxelSize)
            );
            voxelMap[key] = col[i];
        }

        mapDirty = true;
        
        Debug.Log("[OctoMap] " + voxelMap.Count + " puntos cargados.");
    }
}