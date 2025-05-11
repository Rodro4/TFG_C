using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Nav;
using UnityEngine.UI;

namespace RosSharp.RosBridgeClient
{
    public class MapSubscriber : MonoBehaviour
    {
        private RosSocket rosSocket;
        public string topicName = "/map"; // Topic del mapa SLAM
        private OccupancyGrid mapMessage;
        private Texture2D mapTexture;

        public GameObject mapDisplay; // RawImage de Unity en el Canvas para mostrar el mapa
        private bool newMapAvailable = false;

        void Start()
        {
            // Conectarse al RosConnector
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
            // Guardamos el mensaje y marcamos que hay un nuevo mapa para procesar
            mapMessage = message;
            newMapAvailable = true;
        }

        void Update()
        {
            // Procesar el mapa solo desde el hilo principal
            if (newMapAvailable)
            {
                Debug.Log("Procesando nuevo mapa...");
                mapTexture = CreateTextureFromMap(mapMessage);
                UpdateMapDisplay();
                newMapAvailable = false;
            }
        }

        private Texture2D CreateTextureFromMap(OccupancyGrid map)
        {
            int width = (int)map.info.width;
            int height = (int)map.info.height;

            if (map.data.Length != width * height)
            {
                Debug.LogWarning("Datos del mapa inconsistentes con ancho/alto.");
                return null;
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = x + y * width;
                    int value = map.data[index];

                    Color pixelColor;
                    if (value == 0)
                        pixelColor = Color.white; // Libre
                    else if (value == 100)
                        pixelColor = Color.black; // Ocupado
                    else
                        pixelColor = Color.gray; // Desconocido

                    // Invertimos el eje Y porque ROS usa origen abajo a la izquierda
                    texture.SetPixel(x, height - y - 1, pixelColor);
                }
            }

            texture.Apply();
            return texture;
        }

        private void UpdateMapDisplay()
        {
            if (mapDisplay != null && mapTexture != null)
            {
                RawImage image = mapDisplay.GetComponent<RawImage>();
                if (image != null)
                {
                    image.texture = mapTexture;
                    image.SetNativeSize(); // Opcional: ajusta al tamaño real
                }
                else
                {
                    Debug.LogWarning("El GameObject no tiene componente RawImage.");
                }
            }
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
