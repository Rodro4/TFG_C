using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Nav;
using UnityEngine.UI;
using System.Collections.Generic;

namespace RosSharp.RosBridgeClient
{
    // Subscribes to a ROS OccupancyGrid topic, renders the map as a texture, displays it on a UI RawImage, and generates wall GameObjects in the scene
    public class MapSubscriber : MonoBehaviour
    {
        private RosSocket rosSocket;
        public string topicName = "/map";

        private OccupancyGrid mapMessage;
        private Texture2D mapTexture;

        [Tooltip("UI element to display the map texture")]
        public GameObject mapDisplay;

        [Tooltip("Parent transform for the map visualization (walls, quad)")]
        public Transform mapParent;

        private bool newMapAvailable = false;

        // Keep track of instantiated walls to manage them (destroy/update)
        private List<GameObject> walls = new List<GameObject>();

        private GameObject mapQuad;

        void Start()
        {
            RosConnector rosConnector = FindObjectOfType<RosConnector>();
            if (rosConnector != null)
            {
                rosSocket = rosConnector.RosSocket;
                rosSocket.Subscribe<OccupancyGrid>(topicName, ReceiveMap);
                Debug.Log($"MapSubscriber: Subscribed to topic: {topicName}");
            }
            else
            {
                Debug.LogError("MapSubscriber: RosConnector component not found in the scene.");
            }
        }

        // Callback for when a new OccupancyGrid message is received. Marks that a new map is available for processing in Update
        private void ReceiveMap(OccupancyGrid message)
        {
            mapMessage = message;
            newMapAvailable = true;
        }

        void Update()
        {
            // Only process new map when available
            if (newMapAvailable)
            {
                mapTexture = CreateTextureFromMap(mapMessage);
                CreateOrUpdateMapQuad(mapTexture, mapMessage);
                UpdateMapDisplay();
                GenerateWallsFromMap(mapMessage);

                // Rotate the mapParent -90 degrees on Y to align properly
                if (mapParent != null)
                {
                    Vector3 euler = mapParent.rotation.eulerAngles;
                    mapParent.rotation = Quaternion.Euler(euler.x, euler.y - 90f, euler.z);
                }

                newMapAvailable = false;
            }
        }

        // Creates a Texture2D from the OccupancyGrid data. Occupied cells (100) are black, free (0) are white, unknown are gray
        private Texture2D CreateTextureFromMap(OccupancyGrid map)
        {
            int width = (int)map.info.width;
            int height = (int)map.info.height;

            if (map.data.Length != width * height)
            {
                Debug.LogWarning("MapSubscriber: Map data size mismatch.");
                return null;
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            // Iterate over map cells and set pixels accordingly
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = x + y * width;
                    int value = map.data[index];

                    Color pixelColor = value switch
                    {
                        0 => Color.white,       // Free space
                        100 => Color.black,     // Occupied space
                        _ => Color.gray         // Unknown
                    };

                    // Flip Y to match Unity texture coordinates (bottom-left origin
                    texture.SetPixel(x, height - y - 1, pixelColor);
                }
            }

            texture.Apply();
            return texture;
        }

        // Updates the UI RawImage component to show the map texture
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

        // Generates wall GameObjects in the scene corresponding to occupied cells in the map. Each wall is a green cube scaled to the map resolution
        private void GenerateWallsFromMap(OccupancyGrid map)
        {
            // Destroy previous walls before creating new ones
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
                        continue;   // Only occupied cells

                    float worldX = x * resolution + origin.x;
                    float worldZ = y * resolution + origin.z;
                    Vector3 position = new Vector3(worldX, 1f, worldZ);    // y=1 so walls stand above ground

                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = position;
                    wall.transform.localScale = new Vector3(resolution, 2f, resolution);   // height=2 units
                    wall.GetComponent<Renderer>().material.color = Color.green;

                    if (mapParent != null)
                        wall.transform.SetParent(mapParent);

                    walls.Add(wall);
                }
            }
        }

        // Creates or updates a Quad object with the map texture to visualize the map on the XZ plane
        private void CreateOrUpdateMapQuad(Texture2D texture, OccupancyGrid map)
        {
            if (mapQuad == null)
            {
                mapQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mapQuad.name = "MapQuad";
                mapQuad.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Unlit/Texture"));

                if (mapParent != null)
                    mapQuad.transform.SetParent(mapParent);
            }

            MeshRenderer renderer = mapQuad.GetComponent<MeshRenderer>();
            renderer.material.mainTexture = texture;

            float width = map.info.width * map.info.resolution;
            float height = map.info.height * map.info.resolution;

            // Scale the quad to the size of the map, flipped on X to correct orientation
            mapQuad.transform.localScale = new Vector3(-width, height, 1);

            Vector3 origin = new Vector3(
                (float)map.info.origin.position.x,
                0,
                (float)map.info.origin.position.y
            );

            // Position quad so it covers the map area centered on origin + half width/height
            Vector3 position = origin + new Vector3(width / 2f, 0, height / 2f);
            mapQuad.transform.position = position;

            // Rotate quad to lie flat on XZ plane facing upward
            mapQuad.transform.rotation = Quaternion.Euler(90, 180, 0);
        }
    }
}
