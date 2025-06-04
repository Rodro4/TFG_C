//using RosSharp.RosBridgeClient;
//using UnityEngine;

//public class ClickToMapGoal : MonoBehaviour
//{
//    public Camera cam;
//    public OccupancyGridSubscriber gridSubscriber;
//    public SimpleGoalPublisher goalPublisher;
//    public LayerMask mapLayer;
//    public Transform mapTransform; // Transform del plano del mapa

//    void Update()
//    {
//        if (Input.GetMouseButtonDown(0))
//        {
//            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
//            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, mapLayer))
//            {
//                Vector3 localPoint = mapTransform.InverseTransformPoint(hit.point); // Punto en espacio local del mapa
//                Vector2 normalized = new Vector2(localPoint.x + 0.5f, localPoint.z + 0.5f); // local [-0.5, 0.5] -> [0,1]

//                int mapWidth = gridSubscriber.MapWidth;
//                int mapHeight = gridSubscriber.MapHeight;
//                float resolution = gridSubscriber.MapResolution;
//                Vector3 origin = gridSubscriber.MapOrigin;

//                // Coordenadas de celda en el mapa
//                int x = Mathf.FloorToInt(normalized.x * mapWidth);
//                int y = Mathf.FloorToInt(normalized.y * mapHeight);

//                // Verificar si es válido
//                if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight)
//                {
//                    Debug.LogWarning("Clic fuera del mapa.");
//                    return;
//                }

//                int value = gridSubscriber.GetMapCell(new Vector2Int(x, y));
//                if (value != 0)
//                {
//                    Debug.LogWarning("Celda ocupada o desconocida.");
//                    return;
//                }

//                // Convertir a coordenadas ROS (/map)
//                float rosX = origin.x + x * resolution + resolution / 2f;
//                float rosY = origin.z + y * resolution + resolution / 2f;

//                Vector3 rosPosition = new Vector3(rosX, 0f, rosY);

//                Debug.Log($"Publicando objetivo en /map: {rosPosition}");
//                goalPublisher.PublishGoal(rosPosition);
//            }
//        }
//    }
//}
