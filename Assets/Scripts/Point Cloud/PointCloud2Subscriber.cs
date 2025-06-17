//// Usa particle system para mostrar la nube de puntos de rviz en unity. Se ve en vertical y todo rosa.

//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Sensor;

//public class PointCloud2ParticleSubscriber : MonoBehaviour
//{
//    public string pointCloudTopic = "/camera/depth/points"; // Cambia al tópico correcto
//    public int maxPoints = 400000;

//    private RosSocket rosSocket;
//    private ParticleSystem particleSystem;
//    private ParticleSystem.Particle[] particles;

//    // Buffers para puntos
//    private Vector3[] positions;
//    private Color32[] colors;

//    private int currentPointCount = 0;

//    void Start()
//    {
//        var rosConnector = FindObjectOfType<RosConnector>();
//        if (rosConnector == null)
//        {
//            Debug.LogError("RosConnector no encontrado.");
//            enabled = false;
//            return;
//        }

//        rosSocket = rosConnector.RosSocket;

//        rosSocket.Subscribe<PointCloud2>(pointCloudTopic, ReceivePointCloud, 0);

//        // Setup Particle System
//        particleSystem = gameObject.AddComponent<ParticleSystem>();
//        var main = particleSystem.main;
//        main.maxParticles = maxPoints;
//        main.loop = false;
//        main.playOnAwake = false;
//        main.simulationSpace = ParticleSystemSimulationSpace.World;
//        main.startSize = 0.05f;

//        var emission = particleSystem.emission;
//        emission.enabled = false; // Controlaremos las partículas manualmente

//        particles = new ParticleSystem.Particle[maxPoints];
//        positions = new Vector3[maxPoints];
//        colors = new Color32[maxPoints];
//    }

//    private void ReceivePointCloud(PointCloud2 msg)
//    {
//        // Debemos parsear el buffer de datos binarios de PointCloud2
//        // El mensaje contiene metadata sobre el formato: fields, offsets, datatype, etc.

//        int pointStep = (int)msg.point_step; // bytes por punto
//        int rowStep = (int)msg.row_step;
//        int width = (int)msg.width;
//        int height = (int)msg.height;

//        // Campos típicos para XYZ + RGB (o RGB como float32) o intensidad

//        // Buscamos los offsets para "x", "y", "z" y "rgb" o "rgba"
//        int xOffset = -1, yOffset = -1, zOffset = -1, rgbOffset = -1;
//        foreach (var field in msg.fields)
//        {
//            if (field.name == "x") xOffset = (int)field.offset;
//            if (field.name == "y") yOffset = (int)field.offset;
//            if (field.name == "z") zOffset = (int)field.offset;
//            if (field.name == "rgb" || field.name == "rgba") rgbOffset = (int)field.offset;
//        }

//        if (xOffset == -1 || yOffset == -1 || zOffset == -1)
//        {
//            Debug.LogWarning("No se encontraron campos XYZ en PointCloud2.");
//            return;
//        }

//        int totalPoints = width * height;
//        int count = Mathf.Min(totalPoints, maxPoints);

//        int validPoints = 0;
//        for (int i = 0; i < count; i++)
//        {
//            int baseIndex = i * pointStep;

//            float x = BitConverter.ToSingle(msg.data, baseIndex + xOffset);
//            float y = BitConverter.ToSingle(msg.data, baseIndex + yOffset);
//            float z = BitConverter.ToSingle(msg.data, baseIndex + zOffset);

//            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
//                continue;

//            // Filtrar puntos demasiado lejos o cerca si quieres
//            if (z <= 0.01f || z > 10f)
//                continue;

//            positions[validPoints] = new Vector3(x, z, y); // OJO! ajusta ejes según ROS->Unity
//            // Nota: en ROS Z es hacia arriba, en Unity Y es hacia arriba, aquí hacemos swap para que se vea bien
//            // ROS: x->forward, y->left, z->up (depende del frame)
//            // Ajusta según la orientación real del sensor!

//            Color32 col = new Color32(255, 255, 255, 255);

//            if (rgbOffset != -1 && msg.data.Length >= baseIndex + rgbOffset + 4)
//            {
//                // El color está empaquetado como un float (rgba)
//                // Leemos los 4 bytes y los interpretamos como R,G,B,A
//                byte r = msg.data[baseIndex + rgbOffset];
//                byte g = msg.data[baseIndex + rgbOffset + 1];
//                byte b = msg.data[baseIndex + rgbOffset + 2];
//                // byte a = msg.data[baseIndex + rgbOffset + 3]; // alpha ignorado

//                col = new Color32(r, g, b, 255);
//            }

//            colors[validPoints] = col;
//            validPoints++;
//        }

//        currentPointCount = validPoints;

//        // Actualizar partículas (en el siguiente Update para evitar threading issues)
//    }

//    void Update()
//    {
//        if (currentPointCount == 0)
//            return;

//        // Configurar partículas
//        for (int i = 0; i < currentPointCount; i++)
//        {
//            particles[i].position = positions[i];
//            particles[i].startColor = colors[i];
//            particles[i].startSize = 0.05f;
//            particles[i].remainingLifetime = 1f;
//        }

//        particleSystem.SetParticles(particles, currentPointCount);
//    }
//}






////Ya está bien colocado pero sigue rosa
//using System;
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Sensor;

//public class PointCloud2ParticleSubscriber : MonoBehaviour
//{
//    public string pointCloudTopic = "/camera/depth/points";
//    public int maxPoints = 400000;

//    private RosSocket rosSocket;
//    private ParticleSystem particleSystem;
//    private ParticleSystem.Particle[] particles;

//    private Vector3[] positions;
//    private Color32[] colors;

//    private int currentPointCount = 0;

//    void Start()
//    {
//        var rosConnector = FindObjectOfType<RosConnector>();
//        if (rosConnector == null)
//        {
//            Debug.LogError("RosConnector no encontrado.");
//            enabled = false;
//            return;
//        }

//        rosSocket = rosConnector.RosSocket;
//        rosSocket.Subscribe<PointCloud2>(pointCloudTopic, ReceivePointCloud, 0);

//        particleSystem = gameObject.AddComponent<ParticleSystem>();
//        var main = particleSystem.main;
//        main.maxParticles = maxPoints;
//        main.loop = false;
//        main.playOnAwake = false;
//        main.simulationSpace = ParticleSystemSimulationSpace.World;
//        main.startSize = 0.05f;

//        var emission = particleSystem.emission;
//        emission.enabled = false;

//        particles = new ParticleSystem.Particle[maxPoints];
//        positions = new Vector3[maxPoints];
//        colors = new Color32[maxPoints];
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
//        {
//            Debug.LogWarning("No se encontraron campos XYZ en PointCloud2.");
//            return;
//        }

//        int totalPoints = width * height;
//        int count = Mathf.Min(totalPoints, maxPoints);

//        int validPoints = 0;
//        for (int i = 0; i < count; i++)
//        {
//            int baseIndex = i * pointStep;

//            float x = BitConverter.ToSingle(msg.data, baseIndex + xOffset);
//            float y = BitConverter.ToSingle(msg.data, baseIndex + yOffset);
//            float z = BitConverter.ToSingle(msg.data, baseIndex + zOffset);

//            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
//                continue;

//            if (z <= 0.01f || z > 10f)
//                continue;

//            // Corrección de ejes ROS -> Unity
//            positions[validPoints] = new Vector3(x, -y, z);

//            Color32 col = new Color32(255, 255, 255, 255);

//            if (rgbOffset != -1 && msg.data.Length >= baseIndex + rgbOffset + 4)
//            {
//                uint rgba = BitConverter.ToUInt32(msg.data, baseIndex + rgbOffset);
//                byte r = (byte)((rgba >> 16) & 0xFF);
//                byte g = (byte)((rgba >> 8) & 0xFF);
//                byte b = (byte)(rgba & 0xFF);
//                byte a = (byte)((rgba >> 24) & 0xFF);
//                col = new Color32(r, g, b, 255);
//            }

//            colors[validPoints] = col;
//            validPoints++;
//        }

//        currentPointCount = validPoints;
//    }

//    void Update()
//    {
//        if (currentPointCount == 0)
//            return;

//        for (int i = 0; i < currentPointCount; i++)
//        {
//            particles[i].position = positions[i];
//            particles[i].startColor = colors[i];
//            particles[i].startSize = 0.05f;
//            particles[i].remainingLifetime = 1f;
//        }

//        particleSystem.SetParticles(particles, currentPointCount);
//    }
//}













////Ya va lo anterior
//using System;
//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Sensor;

//public class PointCloud2ParticleSubscriber : MonoBehaviour
//{
//    public string pointCloudTopic = "/camera/depth/points";
//    public int maxPoints = 400000;

//    private RosSocket rosSocket;
//    private ParticleSystem particleSystem;
//    private ParticleSystem.Particle[] particles;

//    private Vector3[] positions;
//    private Color32[] colors;

//    private int currentPointCount = 0;

//    void Start()
//    {
//        var rosConnector = FindObjectOfType<RosConnector>();
//        if (rosConnector == null)
//        {
//            Debug.LogError("RosConnector no encontrado.");
//            enabled = false;
//            return;
//        }

//        rosSocket = rosConnector.RosSocket;
//        rosSocket.Subscribe<PointCloud2>(pointCloudTopic, ReceivePointCloud, 0);

//        particleSystem = gameObject.AddComponent<ParticleSystem>();

//        var main = particleSystem.main;
//        main.maxParticles = maxPoints;
//        main.loop = false;
//        main.playOnAwake = false;
//        main.simulationSpace = ParticleSystemSimulationSpace.World;
//        main.startSize = 0.05f;

//        var emission = particleSystem.emission;
//        emission.enabled = false;

//        // Crear y asignar material que soporte color
//        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
//        Material particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
//        particleMaterial.SetColor("_Color", Color.white);
//        renderer.material = particleMaterial;

//        particles = new ParticleSystem.Particle[maxPoints];
//        positions = new Vector3[maxPoints];
//        colors = new Color32[maxPoints];
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
//        {
//            Debug.LogWarning("No se encontraron campos XYZ en PointCloud2.");
//            return;
//        }

//        int totalPoints = width * height;
//        int count = Mathf.Min(totalPoints, maxPoints);

//        int validPoints = 0;
//        for (int i = 0; i < count; i++)
//        {
//            int baseIndex = i * pointStep;

//            float x = BitConverter.ToSingle(msg.data, baseIndex + xOffset);
//            float y = BitConverter.ToSingle(msg.data, baseIndex + yOffset);
//            float z = BitConverter.ToSingle(msg.data, baseIndex + zOffset);

//            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
//                continue;

//            if (z <= 0.01f || z > 10f)
//                continue;

//            // Corrección de ejes ROS -> Unity
//            positions[validPoints] = new Vector3(x, -y, z);

//            Color32 col = new Color32(255, 255, 255, 255);

//            if (rgbOffset != -1 && msg.data.Length >= baseIndex + rgbOffset + 4)
//            {
//                uint rgba = BitConverter.ToUInt32(msg.data, baseIndex + rgbOffset);
//                byte r = (byte)((rgba >> 16) & 0xFF);
//                byte g = (byte)((rgba >> 8) & 0xFF);
//                byte b = (byte)(rgba & 0xFF);
//                byte a = (byte)((rgba >> 24) & 0xFF);
//                col = new Color32(r, g, b, 255);
//            }

//            colors[validPoints] = col;
//            validPoints++;
//        }

//        currentPointCount = validPoints;
//    }

//    void Update()
//    {
//        if (currentPointCount == 0)
//            return;

//        for (int i = 0; i < currentPointCount; i++)
//        {
//            particles[i].position = positions[i];
//            particles[i].startColor = colors[i];
//            particles[i].startSize = 0.05f;
//            particles[i].remainingLifetime = 1f;
//        }

//        particleSystem.SetParticles(particles, currentPointCount);
//    }
//}










//Añadido cambios de detalle (coge 1 de cada x puntos)
using System;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class PointCloud2ParticleSubscriber : MonoBehaviour
{
    public string pointCloudTopic = "/camera/depth/points";

    [Tooltip("Máximo número absoluto de puntos a mostrar")]
    public int maxPoints = 400000;

    [Tooltip("Salto para muestrear puntos. 1=usar todos, 2=1 de cada 2, etc.")]
    public int pointSkip = 10;

    private RosSocket rosSocket;
    private ParticleSystem particleSystem;
    private ParticleSystem.Particle[] particles;

    private Vector3[] positions;
    private Color32[] colors;

    private int currentPointCount = 0;

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
        rosSocket.Subscribe<PointCloud2>(pointCloudTopic, ReceivePointCloud, 0);

        particleSystem = gameObject.AddComponent<ParticleSystem>();

        var main = particleSystem.main;
        main.maxParticles = maxPoints;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize = 0.05f;

        var emission = particleSystem.emission;
        emission.enabled = false;

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        Material particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
        particleMaterial.SetColor("_Color", Color.white);
        renderer.material = particleMaterial;

        particles = new ParticleSystem.Particle[maxPoints];
        positions = new Vector3[maxPoints];
        colors = new Color32[maxPoints];
    }

    private void ReceivePointCloud(PointCloud2 msg)
    {
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
        int maxProcessPoints = maxPoints * pointSkip; // máx puntos a revisar

        int validPoints = 0;

        for (int i = 0; i < totalPoints && validPoints < maxPoints; i += pointSkip)
        {
            if (i >= maxProcessPoints) break; // por seguridad, no pasar de este límite

            int baseIndex = i * pointStep;

            float x = BitConverter.ToSingle(msg.data, baseIndex + xOffset);
            float y = BitConverter.ToSingle(msg.data, baseIndex + yOffset);
            float z = BitConverter.ToSingle(msg.data, baseIndex + zOffset);

            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
                continue;

            if (z <= 0.01f || z > 10f)
                continue;

            // Corrección de ejes ROS -> Unity
            positions[validPoints] = new Vector3(x, -y, z);

            Color32 col = new Color32(255, 255, 255, 255);

            if (rgbOffset != -1 && msg.data.Length >= baseIndex + rgbOffset + 4)
            {
                uint rgba = BitConverter.ToUInt32(msg.data, baseIndex + rgbOffset);
                byte r = (byte)((rgba >> 16) & 0xFF);
                byte g = (byte)((rgba >> 8) & 0xFF);
                byte b = (byte)(rgba & 0xFF);
                col = new Color32(r, g, b, 255);
            }

            colors[validPoints] = col;
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

        particleSystem.SetParticles(particles, currentPointCount);
    }
}



