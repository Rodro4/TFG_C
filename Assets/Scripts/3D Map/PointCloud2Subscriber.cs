////añadido cambios de detalle (coge 1 de cada x puntos)
//using system;
//using unityengine;
//using rossharp.rosbridgeclient;
//using rossharp.rosbridgeclient.messagetypes.sensor;

//public class pointcloud2particlesubscriber : monobehaviour
//{
//    public string pointcloudtopic = "/camera/depth/points";

//    [tooltip("máximo número absoluto de puntos a mostrar")]
//    public int maxpoints = 400000;

//    [tooltip("salto para muestrear puntos. 1=usar todos, 2=1 de cada 2, etc.")]
//    public int pointskip = 10;

//    private rossocket rossocket;
//    private particlesystem particlesystem;
//    private particlesystem.particle[] particles;

//    private vector3[] positions;
//    private color32[] colors;

//    private int currentpointcount = 0;

//    void start()
//    {
//        var rosconnector = findobjectoftype<rosconnector>();
//        if (rosconnector == null)
//        {
//            debug.logerror("rosconnector no encontrado.");
//            enabled = false;
//            return;
//        }

//        rossocket = rosconnector.rossocket;
//        rossocket.subscribe<pointcloud2>(pointcloudtopic, receivepointcloud, 0);

//        particlesystem = gameobject.addcomponent<particlesystem>();

//        var main = particlesystem.main;
//        main.maxparticles = maxpoints;
//        main.loop = false;
//        main.playonawake = false;
//        main.simulationspace = particlesystemsimulationspace.world;
//        main.startsize = 0.05f;

//        var emission = particlesystem.emission;
//        emission.enabled = false;

//        var renderer = particlesystem.getcomponent<particlesystemrenderer>();
//        material particlematerial = new material(shader.find("particles/standard unlit"));
//        particlematerial.setcolor("_color", color.white);
//        renderer.material = particlematerial;

//        particles = new particlesystem.particle[maxpoints];
//        positions = new vector3[maxpoints];
//        colors = new color32[maxpoints];
//    }

//    private void receivepointcloud(pointcloud2 msg)
//    {
//        int pointstep = (int)msg.point_step;
//        int width = (int)msg.width;
//        int height = (int)msg.height;

//        int xoffset = -1, yoffset = -1, zoffset = -1, rgboffset = -1;
//        foreach (var field in msg.fields)
//        {
//            if (field.name == "x") xoffset = (int)field.offset;
//            if (field.name == "y") yoffset = (int)field.offset;
//            if (field.name == "z") zoffset = (int)field.offset;
//            if (field.name == "rgb" || field.name == "rgba") rgboffset = (int)field.offset;
//        }

//        if (xoffset == -1 || yoffset == -1 || zoffset == -1)
//        {
//            debug.logwarning("no se encontraron campos xyz en pointcloud2.");
//            return;
//        }

//        int totalpoints = width * height;
//        int maxprocesspoints = maxpoints * pointskip; // máx puntos a revisar

//        int validpoints = 0;

//        for (int i = 0; i < totalpoints && validpoints < maxpoints; i += pointskip)
//        {
//            if (i >= maxprocesspoints) break; // por seguridad, no pasar de este límite

//            int baseindex = i * pointstep;

//            float x = bitconverter.tosingle(msg.data, baseindex + xoffset);
//            float y = bitconverter.tosingle(msg.data, baseindex + yoffset);
//            float z = bitconverter.tosingle(msg.data, baseindex + zoffset);

//            if (float.isnan(x) || float.isnan(y) || float.isnan(z))
//                continue;

//            if (z <= 0.01f || z > 10f)
//                continue;

//            // corrección de ejes ros -> unity
//            positions[validpoints] = new vector3(x, -y, z);

//            color32 col = new color32(255, 255, 255, 255);

//            if (rgboffset != -1 && msg.data.length >= baseindex + rgboffset + 4)
//            {
//                uint rgba = bitconverter.touint32(msg.data, baseindex + rgboffset);
//                byte r = (byte)((rgba >> 16) & 0xff);
//                byte g = (byte)((rgba >> 8) & 0xff);
//                byte b = (byte)(rgba & 0xff);
//                col = new color32(r, g, b, 255);
//            }

//            colors[validpoints] = col;
//            validpoints++;
//        }

//        currentpointcount = validpoints;
//    }

//    void update()
//    {
//        if (currentpointcount == 0)
//            return;

//        for (int i = 0; i < currentpointcount; i++)
//        {
//            particles[i].position = positions[i];
//            particles[i].startcolor = colors[i];
//            particles[i].startsize = 0.05f;
//            particles[i].remaininglifetime = 1f;
//        }

//        particlesystem.setparticles(particles, currentpointcount);
//    }
//}












//// Particulas persistentes y acumulativas, además se colocan más o menos donde deben. Esto se debe a que usa el URDF del turtlebot, poco preciso
//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Sensor;

//public class PointCloud2ParticleSubscriber : MonoBehaviour
//{
//    public string pointCloudTopic = "/camera/depth/points";
//    public GameObject robot; // Asigna el URDF del robot

//    public float voxelSize = 0.05f;
//    public int maxParticles = 1000000;
//    public int pointSkip = 5;

//    private RosSocket rosSocket;
//    private ParticleSystem particleSystem;
//    private ParticleSystem.Particle[] particles;

//    private Dictionary<Vector3Int, Color32> occupiedVoxels = new();
//    private List<ParticleSystem.Particle> allParticles = new();

//    // Buffer thread-safe para recibir desde ROS
//    private readonly object pointCloudLock = new();
//    private List<(Vector3 localPos, Color32 col)> receivedPoints = new();

//    void Start()
//    {
//        var rosConnector = FindObjectOfType<RosConnector>();
//        if (rosConnector == null)
//        {
//            Debug.LogError("RosConnector no encontrado.");
//            enabled = false;
//            return;
//        }

//        if (robot == null)
//        {
//            Debug.LogError("Asigna el objeto del robot (URDF) al campo 'robot'.");
//            enabled = false;
//            return;
//        }

//        rosSocket = rosConnector.RosSocket;
//        rosSocket.Subscribe<PointCloud2>(pointCloudTopic, ReceivePointCloud, 0);

//        GameObject psGO = new GameObject("PointCloudParticles");
//        psGO.transform.SetParent(robot.transform);
//        psGO.transform.localPosition = Vector3.zero;
//        psGO.transform.localRotation = Quaternion.identity;

//        particleSystem = psGO.AddComponent<ParticleSystem>();
//        var main = particleSystem.main;
//        main.maxParticles = maxParticles;
//        main.loop = false;
//        main.playOnAwake = false;
//        main.simulationSpace = ParticleSystemSimulationSpace.World;
//        main.startSize = voxelSize;

//        var shape = particleSystem.shape;
//        shape.shapeType = ParticleSystemShapeType.Sphere;
//        shape.radius = 1f;

//        var emission = particleSystem.emission;
//        emission.enabled = false;

//        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
//        Material particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
//        particleMaterial.SetColor("_Color", Color.white);
//        renderer.material = particleMaterial;
//    }

//    private void ReceivePointCloud(PointCloud2 msg)
//    {
//        int pointStep = (int)msg.point_step;
//        int width = (int)msg.width;
//        int height = (int)msg.height;

//        int xOffset = -1, yOffset = -1, zOffset = -1, rgbOffset = -1;
//        foreach (var field in msg.fields)
//        {
//            if (field.name == "x") xOffset = (int)field.offset;
//            if (field.name == "y") yOffset = (int)field.offset;
//            if (field.name == "z") zOffset = (int)field.offset;
//            if (field.name == "rgb" || field.name == "rgba") rgbOffset = (int)field.offset;
//        }

//        if (xOffset == -1 || yOffset == -1 || zOffset == -1)
//            return;

//        int totalPoints = Mathf.Min(width * height, 100000); // seguridad

//        var localPoints = new List<(Vector3, Color32)>();

//        for (int i = 0; i < totalPoints; i += pointSkip)
//        {
//            int baseIndex = i * pointStep;
//            if (baseIndex + pointStep > msg.data.Length) continue;

//            float x = BitConverter.ToSingle(msg.data, baseIndex + xOffset);
//            float y = BitConverter.ToSingle(msg.data, baseIndex + yOffset);
//            float z = BitConverter.ToSingle(msg.data, baseIndex + zOffset);

//            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;
//            if (z <= 0.01f || z > 10f) continue;

//            Vector3 localPos = new Vector3(x, -y, z);
//            Color32 col = new Color32(255, 255, 255, 255);

//            if (rgbOffset != -1 && baseIndex + rgbOffset + 4 <= msg.data.Length)
//            {
//                uint rgba = BitConverter.ToUInt32(msg.data, baseIndex + rgbOffset);
//                byte r = (byte)((rgba >> 16) & 0xFF);
//                byte g = (byte)((rgba >> 8) & 0xFF);
//                byte b = (byte)(rgba & 0xFF);
//                col = new Color32(r, g, b, 255);
//            }

//            localPoints.Add((localPos, col));
//        }

//        lock (pointCloudLock)
//        {
//            receivedPoints = localPoints;
//        }
//    }

//    void Update()
//    {
//        List<(Vector3 localPos, Color32 col)> bufferCopy;

//        lock (pointCloudLock)
//        {
//            bufferCopy = new List<(Vector3, Color32)>(receivedPoints);
//            receivedPoints.Clear();
//        }

//        if (bufferCopy.Count == 0)
//            return;

//        Vector3 robotPos = robot.transform.position;
//        Quaternion robotRot = robot.transform.rotation;

//        foreach (var (localPos, col) in bufferCopy)
//        {
//            Vector3 worldPos = robotRot * localPos + robotPos;

//            Vector3Int voxelCoord = new Vector3Int(
//                Mathf.RoundToInt(worldPos.x / voxelSize),
//                Mathf.RoundToInt(worldPos.y / voxelSize),
//                Mathf.RoundToInt(worldPos.z / voxelSize)
//            );

//            if (occupiedVoxels.ContainsKey(voxelCoord))
//                continue;

//            if (allParticles.Count >= maxParticles)
//                break;

//            Vector3 finalPos = (Vector3)voxelCoord * voxelSize;

//            ParticleSystem.Particle p = new ParticleSystem.Particle
//            {
//                position = finalPos,
//                startColor = col,
//                startSize = voxelSize,
//                remainingLifetime = float.MaxValue
//            };

//            allParticles.Add(p);
//            occupiedVoxels[voxelCoord] = col;
//        }

//        if (allParticles.Count > 0)
//        {
//            particles = allParticles.ToArray();
//            particleSystem.SetParticles(particles, particles.Length);
//        }
//    }
//}



































using System;
using System.Collections.Generic;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class PointCloud2ParticleSubscriber : MonoBehaviour
{
    public string pointCloudTopic = "/camera/depth/points";
    public TFSubscriber tfSubscriber;

    [Tooltip("Voxel size used to reduce duplicate points")]
    public float voxelSize = 0.05f;

    [Tooltip("Maximum number of particles allowed in the system")]
    public int maxParticles = 10000;

    [Tooltip("Skip interval for sampling points from the cloud")]
    public int pointSkip = 5;

    private RosSocket rosSocket;
    private ParticleSystem particleSystem;
    private ParticleSystem.Particle[] particles;

    // Keeps track of voxels already filled to avoid duplicate particles
    private Dictionary<Vector3Int, Color32> occupiedVoxels = new();
    private List<ParticleSystem.Particle> allParticles = new();

    void Start()
    {
        var rosConnector = FindObjectOfType<RosConnector>();
        if (rosConnector == null)
        {
            Debug.LogError("PointCloud2ParticleSubscriber: RosConnector not found.");
            enabled = false;
            return;
        }

        if (tfSubscriber == null)
        {
            Debug.LogError("PointCloud2ParticleSubscriber: TFSubscriber not assigned.");
            enabled = false;
            return;
        }

        rosSocket = rosConnector.RosSocket;
        rosSocket.Subscribe<PointCloud2>(pointCloudTopic, ReceivePointCloud, 0);

        // Create an independent GameObject to render the particle system
        GameObject psGO = new GameObject("PointCloudParticles");
        psGO.transform.SetParent(null);
        psGO.transform.position = Vector3.zero;
        psGO.transform.rotation = Quaternion.identity;

        particleSystem = psGO.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.maxParticles = maxParticles;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize = voxelSize;
        main.simulationSpeed = 0f;
        main.startLifetime = float.MaxValue;

        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1f;

        var emission = particleSystem.emission;
        emission.enabled = false;

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        Material particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
        particleMaterial.SetColor("_Color", Color.white);
        renderer.material = particleMaterial;
    }

    // Handles incoming PointCloud2 messages and converts points into particles
    private void ReceivePointCloud(PointCloud2 msg)
    {
        int pointStep = (int)msg.point_step;
        int width = (int)msg.width;
        int height = (int)msg.height;

        // Determine byte offsets for x, y, z and RGB fields
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
            Debug.LogWarning("PointCloud2ParticleSubscriber: XYZ fields not found in PointCloud2.");
            return;
        }

        int totalPoints = width * height;

        Vector3 pos;
        Quaternion rot;
        string cloudFrame = "camera_depth_optical_frame";

        // Attempt to retrieve transformation to 'map' frame
        if (!tfSubscriber.TryGetTransform("map", cloudFrame, out pos, out rot))
        {
            Debug.LogWarning("TF lookup failed for map -> camera_depth_optical_frame.");
            return;
        }

        for (int i = 0; i < totalPoints; i += pointSkip)
        {
            int baseIndex = i * pointStep;
            if (baseIndex + pointStep > msg.data.Length)
                continue;

            float x = BitConverter.ToSingle(msg.data, baseIndex + xOffset);
            float y = BitConverter.ToSingle(msg.data, baseIndex + yOffset);
            float z = BitConverter.ToSingle(msg.data, baseIndex + zOffset);

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
                continue;
            if (z <= 0.01f || z > 10f)
                continue;

            // Convert from ROS FLU to Unity LFU coordinate system
            Vector3 localPos = new Vector3(x, -z, y);

            // Apply TF transformation to convert to world space
            Vector3 worldPos = rot * localPos + pos;

            // Discretize position into voxel coordinates
            Vector3Int voxelCoord = new Vector3Int(
                Mathf.RoundToInt(worldPos.x / voxelSize),
                Mathf.RoundToInt(worldPos.y / voxelSize),
                Mathf.RoundToInt(worldPos.z / voxelSize)
            );

            if (occupiedVoxels.ContainsKey(voxelCoord))
                continue;

            // Default to white if no RGB data
            Color32 col = new Color32(255, 255, 255, 255);

            if (rgbOffset != -1 && baseIndex + rgbOffset + 4 <= msg.data.Length)
            {
                uint rgba = BitConverter.ToUInt32(msg.data, baseIndex + rgbOffset);
                byte r = (byte)((rgba >> 16) & 0xFF);
                byte g = (byte)((rgba >> 8) & 0xFF);
                byte b = (byte)(rgba & 0xFF);
                col = new Color32(r, g, b, 255);
            }

            occupiedVoxels[voxelCoord] = col;

            lock (allParticles)
            {
                if (allParticles.Count < maxParticles)
                {
                    Vector3 finalPos = (Vector3)voxelCoord * voxelSize;
                    ParticleSystem.Particle p = new ParticleSystem.Particle
                    {
                        position = finalPos,
                        startColor = col,
                        startSize = voxelSize,
                        remainingLifetime = float.MaxValue
                    };
                    allParticles.Add(p);
                }
            }
        }
    }

    void Update()
    {
        if (allParticles.Count == 0)
            return;

        ParticleSystem.Particle[] localParticles;

        // Safely copy particles list to avoid race conditions
        lock (allParticles)
        {
            localParticles = allParticles.ToArray();
        }

        particleSystem.SetParticles(localParticles, localParticles.Length);
        particleSystem.Play();
    }
}