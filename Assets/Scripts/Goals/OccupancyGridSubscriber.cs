//using UnityEngine;
//using RosSharp.RosBridgeClient;
//using System;

//namespace RosSharp.RosBridgeClient
//{
//    [RequireComponent(typeof(RosConnector))]
//    public class OccupancyGridSubscriber : UnitySubscriber<RosSharp.RosBridgeClient.MessageTypes.Nav.OccupancyGrid>
//    {
//        public string MapFrame = "map";

//        public float MapResolution => resolution;
//        public int MapWidth => width;
//        public int MapHeight => height;
//        public Vector3 MapOrigin => origin;


//        private int[] mapData;
//        private float resolution;
//        private int width;
//        private int height;
//        private Vector3 origin; // posición del mapa en coordenadas de Unity (desde ROS)

//        private bool mapReceived = false;

//        protected override void Start()
//        {
//            base.Start();
//        }

//        protected override void ReceiveMessage(MessageTypes.Nav.OccupancyGrid message)
//        {
//            resolution = message.info.resolution;
//            width = (int)message.info.width;
//            height = (int)message.info.height;

//            mapData = Array.ConvertAll(message.data, x => (int)x);

//            // Posición del origen del mapa en mundo ROS
//            origin = new Vector3(
//                (float)message.info.origin.position.x,
//                0,
//                (float)message.info.origin.position.y
//            );

//            mapReceived = true;
//        }

//        /// <summary>
//        /// Convierte posición en Unity (X,Z) a índice del mapa
//        /// </summary>
//        public Vector2Int? WorldToMap(Vector3 worldPos)
//        {
//            if (!mapReceived)
//                return null;

//            // Convertimos mundo Unity a coordenadas ROS en plano XY
//            float rosX = worldPos.x;
//            float rosY = worldPos.z;

//            float relativeX = rosX - origin.x;
//            float relativeY = rosY - origin.z;

//            int mapX = Mathf.FloorToInt(relativeX / resolution);
//            int mapY = Mathf.FloorToInt(relativeY / resolution);

//            if (mapX < 0 || mapX >= width || mapY < 0 || mapY >= height)
//                return null;

//            return new Vector2Int(mapX, mapY);
//        }

//        /// <summary>
//        /// Devuelve el valor de una celda del mapa. -1=desconocido, 0=libre, 100=ocupado
//        /// </summary>
//        public int GetMapCell(Vector2Int mapCoord)
//        {
//            if (!mapReceived || mapData == null)
//                return -1;

//            int index = mapCoord.y * width + mapCoord.x;
//            if (index < 0 || index >= mapData.Length)
//                return -1;

//            return mapData[index];
//        }

//        public bool IsReady()
//        {
//            return mapReceived;
//        }
//    }
//}
