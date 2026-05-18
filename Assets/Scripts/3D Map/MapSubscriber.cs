// using UnityEngine;
// using RosSharp.RosBridgeClient;
// using RosSharp.RosBridgeClient.MessageTypes.Nav;
// using UnityEngine.UI;
// using System.Collections.Generic;

// namespace RosSharp.RosBridgeClient
// {
//     // Subscribes to a ROS OccupancyGrid topic, renders the map as a texture, displays it on a UI RawImage, and generates wall GameObjects in the scene
//     public class MapSubscriber : MonoBehaviour
//     {
//         private RosSocket rosSocket;
//         public string topicName = "/map";

//         private OccupancyGrid mapMessage;
//         private Texture2D mapTexture;

//         [Tooltip("UI element to display the map texture")]
//         public GameObject mapDisplay;

//         [Tooltip("Parent transform for the map visualization (walls, quad)")]
//         public Transform mapParent;

//         private bool newMapAvailable = false;

//         // Keep track of instantiated walls to manage them (destroy/update)
//         private List<GameObject> walls = new List<GameObject>();

//         private GameObject mapQuad;

//         void Start()
//         {
//             RosConnector rosConnector = FindObjectOfType<RosConnector>();
//             if (rosConnector != null)
//             {
//                 rosSocket = rosConnector.RosSocket;
//                 rosSocket.Subscribe<OccupancyGrid>(topicName, ReceiveMap);
//                 Debug.Log($"MapSubscriber: Subscribed to topic: {topicName}");
//             }
//             else
//             {
//                 Debug.LogError("MapSubscriber: RosConnector component not found in the scene.");
//             }
//         }

//         // Callback for when a new OccupancyGrid message is received. Marks that a new map is available for processing in Update
//         private void ReceiveMap(OccupancyGrid message)
//         {
//             mapMessage = message;
//             newMapAvailable = true;
//         }

//         void Update()
//         {
//             // Only process new map when available
//             if (newMapAvailable)
//             {
//                 mapTexture = CreateTextureFromMap(mapMessage);
//                 CreateOrUpdateMapQuad(mapTexture, mapMessage);
//                 UpdateMapDisplay();
//                 GenerateWallsFromMap(mapMessage);

//                 // Rotate the mapParent -90 degrees on Y to align properly
//                 if (mapParent != null)
//                 {
//                     Vector3 euler = mapParent.rotation.eulerAngles;
//                     mapParent.rotation = Quaternion.Euler(euler.x, euler.y - 90f, euler.z);
//                 }

//                 newMapAvailable = false;
//             }
//         }

//         // Creates a Texture2D from the OccupancyGrid data. Occupied cells (100) are black, free (0) are white, unknown are gray
//         private Texture2D CreateTextureFromMap(OccupancyGrid map)
//         {
//             int width = (int)map.info.width;
//             int height = (int)map.info.height;

//             if (map.data.Length != width * height)
//             {
//                 Debug.LogWarning("MapSubscriber: Map data size mismatch.");
//                 return null;
//             }

//             Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);

//             // Iterate over map cells and set pixels accordingly
//             for (int y = 0; y < height; y++)
//             {
//                 for (int x = 0; x < width; x++)
//                 {
//                     int index = x + y * width;
//                     int value = map.data[index];

//                     Color pixelColor = value switch
//                     {
//                         0 => Color.white,       // Free space
//                         100 => Color.black,     // Occupied space
//                         _ => Color.gray         // Unknown
//                     };

//                     // Flip Y to match Unity texture coordinates
//                     texture.SetPixel(x, height - y - 1, pixelColor);
//                 }
//             }

//             texture.Apply();
//             return texture;
//         }

//         // Updates the UI RawImage component to show the map texture
//         private void UpdateMapDisplay()
//         {
//             if (mapDisplay == null || mapTexture == null) return;

//             RawImage image = mapDisplay.GetComponent<RawImage>();
//             if (image != null)
//             {
//                 image.texture = mapTexture;
//                 image.SetNativeSize();
//             }
//         }

//         // Generates wall GameObjects in the scene corresponding to occupied cells in the map. Each wall is a green cube scaled to the map resolution
//         private void GenerateWallsFromMap(OccupancyGrid map)
//         {
//             // Destroy previous walls before creating new ones
//             foreach (GameObject wall in walls)
//                 Destroy(wall);
//             walls.Clear();

//             int width = (int)map.info.width;
//             int height = (int)map.info.height;
//             float resolution = map.info.resolution;
//             Vector3 origin = new Vector3(
//                 (float)map.info.origin.position.x,
//                 0,
//                 (float)map.info.origin.position.y
//             );

//             for (int y = 0; y < height; y++)
//             {
//                 for (int x = 0; x < width; x++)
//                 {
//                     int index = x + y * width;
//                     if (map.data[index] != 100)
//                         continue;   // Only occupied cells

//                     float worldX = x * resolution + origin.x;
//                     float worldZ = y * resolution + origin.z;
//                     Vector3 position = new Vector3(worldX, 1f, worldZ);    // y=1 so walls stand above ground

//                     GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
//                     wall.transform.position = position;
//                     wall.transform.localScale = new Vector3(resolution, 2f, resolution);   // height=2 units
//                     wall.GetComponent<Renderer>().material.color = Color.green;

//                     if (mapParent != null)
//                         wall.transform.SetParent(mapParent);

//                     walls.Add(wall);
//                 }
//             }
//         }

//         // Creates or updates a Quad object with the map texture to visualize the map on the XZ plane
//         private void CreateOrUpdateMapQuad(Texture2D texture, OccupancyGrid map)
//         {
//             if (mapQuad == null)
//             {
//                 mapQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
//                 mapQuad.name = "MapQuad";
//                 mapQuad.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Unlit/Texture"));

//                 if (mapParent != null)
//                     mapQuad.transform.SetParent(mapParent);
//             }

//             MeshRenderer renderer = mapQuad.GetComponent<MeshRenderer>();
//             renderer.material.mainTexture = texture;

//             float width = map.info.width * map.info.resolution;
//             float height = map.info.height * map.info.resolution;

//             // Scale the quad to the size of the map, flipped on X to correct orientation
//             mapQuad.transform.localScale = new Vector3(-width, height, 1);

//             Vector3 origin = new Vector3(
//                 (float)map.info.origin.position.x,
//                 0,
//                 (float)map.info.origin.position.y
//             );

//             // Position quad so it covers the map area centered on origin + half width/height
//             Vector3 position = origin + new Vector3(width / 2f, 0, height / 2f);
//             mapQuad.transform.position = position;

//             // Rotate quad to lie flat on XZ plane facing upward
//             mapQuad.transform.rotation = Quaternion.Euler(90, 180, 0);
//         }
//     }
// }


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

        [Tooltip("UI element to display the map texture")]
        public GameObject mapDisplay;

        [Tooltip("Parent transform for the map visualization (walls, quad)")]
        public Transform mapParent;

        private bool newMapAvailable = false;

        // Pool de cubos: activos e inactivos
        private List<GameObject> activeWalls  = new List<GameObject>();
        private List<GameObject> inactivePool = new List<GameObject>();

        private GameObject mapQuad;

        // Material compartido para todos los cubos (evita crear uno por cubo)
        private Material wallMaterial;

        void Start()
        {
            wallMaterial = new Material(Shader.Find("Standard"));
            wallMaterial.color = Color.green;

            RosConnector rosConnector = FindObjectOfType<RosConnector>();
            if (rosConnector != null)
            {
                rosSocket = rosConnector.RosSocket;
                rosSocket.Subscribe<OccupancyGrid>(topicName, ReceiveMap);
                Debug.Log($"MapSubscriber: Subscribed to topic: {topicName}");
            }
            else
            {
                Debug.LogError("MapSubscriber: RosConnector not found.");
            }
        }

        private void ReceiveMap(OccupancyGrid message)
        {
            mapMessage = message;
            newMapAvailable = true;
        }

        void Update()
        {
            if (!newMapAvailable) return;

            mapTexture = CreateTextureFromMap(mapMessage);
            CreateOrUpdateMapQuad(mapTexture, mapMessage);
            UpdateMapDisplay();
            GenerateWallsFromMap(mapMessage);

            if (mapParent != null)
            {
                Vector3 euler = mapParent.rotation.eulerAngles;
                mapParent.rotation = Quaternion.Euler(euler.x, euler.y - 90f, euler.z);
            }

            newMapAvailable = false;
        }

        // ─── POOL ────────────────────────────────────────────────────
        // Devuelve un cubo del pool (o lo crea si el pool está vacío)
        private GameObject GetWall()
        {
            if (inactivePool.Count > 0)
            {
                int last = inactivePool.Count - 1;
                GameObject go = inactivePool[last];
                inactivePool.RemoveAt(last);
                go.SetActive(true);
                return go;
            }

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // Desactivar el collider: no lo necesitas en el mapa 2D→3D
            Destroy(wall.GetComponent<Collider>());
            wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            if (mapParent != null) wall.transform.SetParent(mapParent);
            return wall;
        }

        // Devuelve todos los cubos activos al pool (sin Destroy)
        private void ReturnAllToPool()
        {
            foreach (var w in activeWalls)
            {
                w.SetActive(false);
                inactivePool.Add(w);
            }
            activeWalls.Clear();
        }
        // ─────────────────────────────────────────────────────────────

        private void GenerateWallsFromMap(OccupancyGrid map)
        {
            // Devolver los cubos anteriores al pool en vez de destruirlos
            ReturnAllToPool();

            int width      = (int)map.info.width;
            int height     = (int)map.info.height;
            float res      = map.info.resolution;
            Vector3 origin = new Vector3(
                (float)map.info.origin.position.x,
                0,
                (float)map.info.origin.position.y
            );

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (map.data[x + y * width] != 100) continue;

                    GameObject wall = GetWall();
                    wall.transform.position   = new Vector3(x * res + origin.x, 1f, y * res + origin.z);
                    wall.transform.localScale = new Vector3(res, 2f, res);

                    activeWalls.Add(wall);
                }
            }
        }

        // ─── Sin cambios respecto al original ────────────────────────
        private Texture2D CreateTextureFromMap(OccupancyGrid map)
        {
            int width  = (int)map.info.width;
            int height = (int)map.info.height;

            if (map.data.Length != width * height)
            {
                Debug.LogWarning("MapSubscriber: Map data size mismatch.");
                return null;
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Color c = map.data[x + y * width] switch
                    {
                        0   => Color.white,
                        100 => Color.black,
                        _   => Color.gray
                    };
                    texture.SetPixel(x, height - y - 1, c);
                }

            texture.Apply();
            return texture;
        }

        private void UpdateMapDisplay()
        {
            if (mapDisplay == null || mapTexture == null) return;
            RawImage image = mapDisplay.GetComponent<RawImage>();
            if (image != null) { image.texture = mapTexture; image.SetNativeSize(); }
        }

        private void CreateOrUpdateMapQuad(Texture2D texture, OccupancyGrid map)
        {
            if (mapQuad == null)
            {
                mapQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mapQuad.name = "MapQuad";
                mapQuad.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Unlit/Texture"));
                if (mapParent != null) mapQuad.transform.SetParent(mapParent);
            }

            mapQuad.GetComponent<MeshRenderer>().material.mainTexture = texture;

            float w = map.info.width  * map.info.resolution;
            float h = map.info.height * map.info.resolution;
            mapQuad.transform.localScale = new Vector3(-w, h, 1);

            Vector3 origin = new Vector3(
                (float)map.info.origin.position.x, 0,
                (float)map.info.origin.position.y);
            mapQuad.transform.position = origin + new Vector3(w / 2f, 0, h / 2f);
            mapQuad.transform.rotation = Quaternion.Euler(90, 180, 0);
        }
    }
}