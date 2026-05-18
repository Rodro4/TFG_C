using System;
using System.Collections.Generic;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class OctoMapPointCloudSubscriber : MonoBehaviour
{
    [Header("ROS")]
    public string PointCloudTopic = "/octomap_point_cloud_centers";
    public bool useROS = true; // false = solo cargar mapa guardado

    [Header("Rendimiento")]
    public int MaxPoints = 200000;
    public int PointSkip = 1;
    public float UpdateInterval = 0.5f; // segundos entre actualizaciones de partículas

    [Header("Partículas")]
    public float particleSize = 0.05f;

    [Header("Fallback por altura (si no hay color RGB)")]
    public bool useHeightColorFallback = true;
    public float alturaMin = 0f;
    public float alturaMax = 1.25f;
    public Gradient miGradiente;

    [Header("Colliders")]
    public bool generateColliders = false;
    public int collidersPerFrame = 20;

    // --- Internos ---
    private RosSocket rosSocket;
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    private Vector3[] positions;
    private Color32[] colors;
    private int currentPointCount = 0;

    private bool dataReady = false;
    private float timeSinceLastUpdate = 0f;

    private Queue<int> pendingColliderIndices = new Queue<int>();
    private List<GameObject> colliderObjects = new List<GameObject>();

    void Start()
    {
        InitParticleSystem();

        particles = new ParticleSystem.Particle[MaxPoints];
        positions = new Vector3[MaxPoints];
        colors    = new Color32[MaxPoints];

        if (useROS)
        {
            var rosConnector = FindObjectOfType<RosConnector>();
            if (rosConnector == null)
            {
                Debug.LogError("[OctoMap] RosConnector no encontrado.");
                return;
            }
            rosSocket = rosConnector.RosSocket;
            rosSocket.Subscribe<PointCloud2>(PointCloudTopic, ReceivePointCloud, 0);
            Debug.Log("[OctoMap] Suscrito a " + PointCloudTopic);
        }
        else
        {
            Debug.Log("[OctoMap] Modo offline - carga un mapa guardado.");
        }
    }

    void InitParticleSystem()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles    = MaxPoints;
        main.loop            = false;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize       = particleSize;
        main.startLifetime   = float.MaxValue;

        var emission = ps.emission;
        emission.enabled = false;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material   = new Material(Shader.Find("Particles/Standard Unlit"));
        r.renderMode = ParticleSystemRenderMode.Billboard;
    }

    // =========================================================
    // ROS RECEIVE
    // =========================================================
    private void ReceivePointCloud(PointCloud2 msg)
    {
        // Check robusto: acepta si este GO o cualquier padre es el mapa activo
        // var mapManager = FindObjectOfType<Map3DManager>();
        // if (mapManager != null)
        // {
        //     GameObject active = mapManager.activeMap;
        //     bool isActive = (active == gameObject) ||
        //                     (active != null && transform.IsChildOf(active.transform));
        //     if (!isActive) return;
        // }

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

        bool hasColor   = rgbOff != -1;
        int validPoints = 0;

        for (int i = 0; i < totalPoints && validPoints < MaxPoints; i += PointSkip)
        {
            int idx = i * pointStep;

            float rx = BitConverter.ToSingle(msg.data, idx + xOff);
            float ry = BitConverter.ToSingle(msg.data, idx + yOff);
            float rz = BitConverter.ToSingle(msg.data, idx + zOff);

            if (float.IsNaN(rx) || float.IsNaN(ry) || float.IsNaN(rz)) continue;

            positions[validPoints] = new Vector3(-ry, rz, rx);

            if (hasColor)
            {
                uint rgb = BitConverter.ToUInt32(msg.data, idx + rgbOff);
                colors[validPoints] = new Color32(
                    (byte)((rgb >> 16) & 0xFF),
                    (byte)((rgb >> 8)  & 0xFF),
                    (byte)( rgb        & 0xFF),
                    255
                );
            }
            else if (useHeightColorFallback)
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(alturaMin, alturaMax, rz));
                colors[validPoints] = (Color32)miGradiente.Evaluate(t);
                Color c = miGradiente.Evaluate(t);
                c.a = 1f;
                colors[validPoints] = (Color32)c;
            }
            else
            {
                colors[validPoints] = Color.white;
            }

            validPoints++;

            if (generateColliders)
                pendingColliderIndices.Enqueue(validPoints - 1);
        }

        currentPointCount = validPoints;
        dataReady = true;
    }

    void GenerateColliders()
    {
        int created = 0;
        while (pendingColliderIndices.Count > 0 && created < collidersPerFrame)
        {
            int idx = pendingColliderIndices.Dequeue();
            if (idx >= currentPointCount) continue;

            GameObject go = new GameObject("OctoCollider");
            go.transform.SetParent(transform);
            go.transform.position = positions[idx];
            go.AddComponent<BoxCollider>().size = Vector3.one * particleSize;
            colliderObjects.Add(go);
            created++;
        }
    }

    // =========================================================
    // UPDATE con throttle para evitar picos de FPS
    // =========================================================
    void Update()
    {
        if (!dataReady) return;

        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate < UpdateInterval) return;

        timeSinceLastUpdate = 0f;
        dataReady = false;

        for (int i = 0; i < currentPointCount; i++)
        {
            particles[i].position          = positions[i];
            particles[i].startColor        = colors[i];
            particles[i].startSize         = particleSize;
            particles[i].remainingLifetime = float.MaxValue;
        }

        ps.SetParticles(particles, currentPointCount);

        if (generateColliders) GenerateColliders();
    }

    // =========================================================
    // API PÚBLICA para MapSaverLoaderManager
    // =========================================================
    public (Vector3[] pos, Color32[] col, int count) GetCurrentPoints()
    {
        return (positions, colors, currentPointCount);
    }

    public void LoadPoints(Vector3[] pos, Color32[] col, int count)
    {
        currentPointCount = Mathf.Min(count, MaxPoints);

        for (int i = 0; i < currentPointCount; i++)
        {
            positions[i] = pos[i];
            colors[i]    = col[i];
            particles[i].position          = pos[i];
            particles[i].startColor        = col[i];
            particles[i].startSize         = particleSize;
            particles[i].remainingLifetime = float.MaxValue;
        }

        ps.SetParticles(particles, currentPointCount);
        dataReady = false;
        Debug.Log("[OctoMap] " + currentPointCount + " puntos cargados desde archivo.");
    }

    public void ClearPoints()
    {
        currentPointCount = 0;
        ps.SetParticles(particles, 0);
        dataReady = false;
        foreach (var go in colliderObjects) Destroy(go);
        colliderObjects.Clear();
        pendingColliderIndices.Clear();
    }
}