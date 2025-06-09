using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Nav;
using UnityEngine.UI;
using System.Collections.Generic;

namespace RosSharp.RosBridgeClient
{
    public class MapSubscriber : MonoBehaviour
    {
        private RosSocket rosSocket;
        public string topicName = "/map";
        private OccupancyGrid mapMessage;
        private Texture2D mapTexture;

        public GameObject mapDisplay;
        public Transform mapParent; // Objeto contenedor para el mapa y muros

        private bool newMapAvailable = false;
        private List<GameObject> walls = new List<GameObject>();

        private GameObject mapQuad;

        void Start()
        {
            RosConnector rosConnector = FindObjectOfType<RosConnector>();
            if (rosConnector != null)
            {
                rosSocket = rosConnector.RosSocket;
                rosSocket.Subscribe<OccupancyGrid>(topicName, ReceiveMap);
                Debug.Log("Suscrito al topic: " + topicName);
            }
            else
            {
                Debug.LogError("No se encontró el componente RosConnector.");
            }
        }

        private void ReceiveMap(OccupancyGrid message)
        {
            mapMessage = message;
            newMapAvailable = true;
        }

        void Update()
        {
            if (newMapAvailable)
            {
                mapTexture = CreateTextureFromMap(mapMessage);
                CreateOrUpdateMapQuad(mapTexture, mapMessage);
                UpdateMapDisplay();
                GenerateWallsFromMap(mapMessage);

                // ROTAR MAP PARENT en Y solo después de tener el mapa generado
                if (mapParent != null)
                {
                    Vector3 euler = mapParent.rotation.eulerAngles;
                    mapParent.rotation = Quaternion.Euler(euler.x, euler.y - 90f, euler.z);
                }

                newMapAvailable = false;
            }
        }

        private Texture2D CreateTextureFromMap(OccupancyGrid map)
        {
            int width = (int)map.info.width;
            int height = (int)map.info.height;

            if (map.data.Length != width * height)
            {
                Debug.LogWarning("Datos del mapa inconsistentes.");
                return null;
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = x + y * width;
                    int value = map.data[index];

                    Color pixelColor = value switch
                    {
                        0 => Color.white,
                        100 => Color.black,
                        _ => Color.gray
                    };

                    texture.SetPixel(x, height - y - 1, pixelColor);
                }
            }

            texture.Apply();
            return texture;
        }

        private void UpdateMapDisplay()
        {
            if (mapDisplay == null || mapTexture == null) return;

            RawImage image = mapDisplay.GetComponent<RawImage>();
            if (image != null)
            {
                image.texture = mapTexture;
                image.SetNativeSize();
            }
        }

        private void GenerateWallsFromMap(OccupancyGrid map)
        {
            foreach (GameObject wall in walls)
                Destroy(wall);
            walls.Clear();

            int width = (int)map.info.width;
            int height = (int)map.info.height;
            float resolution = map.info.resolution;
            Vector3 origin = new Vector3(
                (float)map.info.origin.position.x,
                0,
                (float)map.info.origin.position.y
            );

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = x + y * width;
                    if (map.data[index] != 100)
                        continue;

                    float worldX = x * resolution + origin.x;
                    float worldZ = y * resolution + origin.z;
                    Vector3 position = new Vector3(worldX, 1f, worldZ);

                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = position;
                    wall.transform.localScale = new Vector3(resolution, 2f, resolution);
                    wall.GetComponent<Renderer>().material.color = Color.green;

                    // Añadir a mapParent
                    if (mapParent != null)
                        wall.transform.SetParent(mapParent);

                    walls.Add(wall);
                }
            }
        }

        private void CreateOrUpdateMapQuad(Texture2D texture, OccupancyGrid map)
        {
            if (mapQuad == null)
            {
                mapQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mapQuad.name = "MapQuad";
                mapQuad.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Unlit/Texture"));

                // Hacer que mapQuad sea hijo de mapParent
                if (mapParent != null)
                    mapQuad.transform.SetParent(mapParent);
            }

            MeshRenderer renderer = mapQuad.GetComponent<MeshRenderer>();
            renderer.material.mainTexture = texture;

            float width = map.info.width * map.info.resolution;
            float height = map.info.height * map.info.resolution;

            mapQuad.transform.localScale = new Vector3(-width, height, 1);

            Vector3 origin = new Vector3(
                (float)map.info.origin.position.x,
                0,
                (float)map.info.origin.position.y
            );

            Vector3 position = origin + new Vector3(width / 2f, 0, height / 2f);
            mapQuad.transform.position = position;

            // El Quad se gira para que esté sobre el plano XZ
            mapQuad.transform.rotation = Quaternion.Euler(90, 180, 0);
        }

        void OnApplicationQuit()
        {
            if (rosSocket != null)
            {
                rosSocket.Close();
                Debug.Log("Conexión ROS cerrada.");
            }
        }
    }
}
