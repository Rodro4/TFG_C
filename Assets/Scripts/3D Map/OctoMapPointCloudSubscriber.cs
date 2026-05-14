using System;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class OctoMapPointCloudSubscriber : MonoBehaviour
{
    public string PointCloudTopic = "/octomap_point_cloud_centers";

    [Tooltip("M�ximo n�mero absoluto de puntos a mostrar")]
    public int MaxPoints = 400000;

    [Tooltip("Salto para muestrear puntos. 1 = usar todos, 2 = 1 de cada 2, etc")]
    public int PointSkip = 10;

    private RosSocket rosSocket;
    private ParticleSystem ParticleSystem;
    private ParticleSystem.Particle[] particles;

    private Vector3[] positions;
    private Color32[] colors;

    private int currentPointCount = 0;

    [Header("Altura para color")]
    public float alturaMin = 0f;
    public float alturaMax = 1.25f;

    [Header("Gradiente por altura")]
    public Gradient miGradiente;


    void Start()
    {
        var rosConnector = FindObjectOfType<RosConnector>();
        if (rosConnector == null)
        {
            Debug.LogError("RosConnector no encontrado.");
            enabled = false;
            return;
        }

        rosSocket = rosConnector.RosSocket;
        rosSocket.Subscribe<PointCloud2>(PointCloudTopic, ReceivePointCloud, 0);

        ParticleSystem = gameObject.AddComponent<ParticleSystem>();

        var main = ParticleSystem.main;
        main.maxParticles = MaxPoints;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize = 0.05f;

        var emission = ParticleSystem.emission;
        emission.enabled = false;

        var renderer = ParticleSystem.GetComponent<ParticleSystemRenderer>();
        Material particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
        particleMaterial.SetColor("_Color", Color.white);
        renderer.material = particleMaterial;

        particles = new ParticleSystem.Particle[MaxPoints];
        positions = new Vector3[MaxPoints];
        colors = new Color32[MaxPoints];
    }

    private void ReceivePointCloud(PointCloud2 msg)
    {
        var mapManager = FindObjectOfType<Map3DManager>();
        if (mapManager.activeMap != gameObject)
            return;  // Ignora si este mapa no está activo

        int pointStep = (int)msg.point_step;
        int width = (int)msg.width;
        int height = (int)msg.height;

        int xOffset = -1, yOffset = -1, zOffset = -1, rgbOffset = -1;
        foreach (var field in msg.fields)
        {
            if (field.name == "x") xOffset = (int)field.offset;
            if (field.name == "y") yOffset = (int)field.offset;
            if (field.name == "z") zOffset = (int)field.offset;
            if (field.name == "rgb" || field.name == "rgba") rgbOffset = (int)field.offset;
        }

        if (xOffset == -1 || yOffset == -1 || zOffset == -1)
        {
            Debug.LogWarning("No se encontraron campos XYZ en PointCloud2.");
            return;
        }

        int totalPoints = width * height;
        int maxProcessPoints = MaxPoints * PointSkip;

        int validPoints = 0;

        for (int i = 0; i < totalPoints && validPoints < MaxPoints; i += PointSkip)
        {
            if (i >= maxProcessPoints) break;

            int baseIndex = i * pointStep;

            float x = BitConverter.ToSingle(msg.data, baseIndex + xOffset);
            float y = BitConverter.ToSingle(msg.data, baseIndex + yOffset);
            float z = BitConverter.ToSingle(msg.data, baseIndex + zOffset);

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
                continue;

            if (z <= 0.01f || z > 10f)
                continue;

            Vector3 worldPos = new Vector3(x, y, z);
            positions[validPoints] = new Vector3(-worldPos.y, worldPos.z, worldPos.x);

            // Altura real del OctoMap (ROS Z)
            float h = worldPos.z;

            // Normalizar altura a 0-1
            float t = Mathf.InverseLerp(alturaMin, alturaMax, h);

            // Por si acaso clampeamos entre 0 y 1
            t = Mathf.Clamp01(t);

            Color col = miGradiente.Evaluate(t);

            colors[validPoints] = (Color32)col;

            validPoints++;
        }

        currentPointCount = validPoints;
    }

    void Update()
    {
        if (currentPointCount == 0)
            return;

        for (int i = 0; i < currentPointCount; i++)
        {
            particles[i].position = positions[i];
            particles[i].startColor = colors[i];
            particles[i].startSize = 0.05f;
            particles[i].remainingLifetime = 1f;
        }

        ParticleSystem.SetParticles(particles, currentPointCount);
    }
}