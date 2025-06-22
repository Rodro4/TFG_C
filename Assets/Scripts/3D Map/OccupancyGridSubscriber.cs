using UnityEngine;
using RosSharp.RosBridgeClient;
using System;

namespace RosSharp.RosBridgeClient
{

    // Subscribes to a ROS OccupancyGrid message and converts it for Unity usage. Provides methods to query map info and convert between Unity world coords and map indices
    [RequireComponent(typeof(RosConnector))]
    public class OccupancyGridSubscriber : UnitySubscriber<RosSharp.RosBridgeClient.MessageTypes.Nav.OccupancyGrid>
    {
        [Tooltip("The coordinate frame of the map, usually 'map'")]
        public string MapFrame = "map";

        // Public accessors for map properties
        public float MapResolution => resolution;
        public int MapWidth => width;
        public int MapHeight => height;
        public Vector3 MapOrigin => origin;


        private int[] mapData;
        private float resolution;
        private int width;
        private int height;
        private Vector3 origin;

        private bool mapReceived = false;

        protected override void Start()
        {
            base.Start();
        }

        // Callback invoked when an OccupancyGrid message is received. Parses and stores map info and occupancy data
        protected override void ReceiveMessage(MessageTypes.Nav.OccupancyGrid message)
        {
            resolution = message.info.resolution;
            width = (int)message.info.width;
            height = (int)message.info.height;

            // Convert occupancy data from byte[] to int[]
            mapData = Array.ConvertAll(message.data, x => (int)x);

            // Map origin position in Unity's coordinate system (x,z plane)
            origin = new Vector3(
                (float)message.info.origin.position.x,
                0,
                (float)message.info.origin.position.y
            );

            mapReceived = true;
        }

        // Converts a Unity world position (x,z) into map grid coordinates. Returns null if position is outside the map bounds or map not received yet
        public Vector2Int? WorldToMap(Vector3 worldPos)
        {
            if (!mapReceived)
                return null;

            // Unity uses X,Z for horizontal plane; ROS map origin is in X,Y plane
            float rosX = worldPos.x;
            float rosY = worldPos.z;

            // Position relative to map origin
            float relativeX = rosX - origin.x;
            float relativeY = rosY - origin.z;

            int mapX = Mathf.FloorToInt(relativeX / resolution);
            int mapY = Mathf.FloorToInt(relativeY / resolution);

            // Check if inside map bounds
            if (mapX < 0 || mapX >= width || mapY < 0 || mapY >= height)
                return null;

            return new Vector2Int(mapX, mapY);
        }

        // Returns occupancy value of a cell at map coordinates.-1 = unknown, 0 = free, 100 = occupied. Returns -1 if coordinates out of range or map data unavailable
        public int GetMapCell(Vector2Int mapCoord)
        {
            if (!mapReceived || mapData == null)
                return -1;

            int index = mapCoord.y * width + mapCoord.x;
            if (index < 0 || index >= mapData.Length)
                return -1;

            return mapData[index];
        }
    }
}
