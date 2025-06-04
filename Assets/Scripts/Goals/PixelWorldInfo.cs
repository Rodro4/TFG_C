using UnityEngine;
using RosSharp.RosBridgeClient;

public class PixelWorldInfo : MonoBehaviour
{
    public DepthImageSubscriber depthImageSubscriber; // Referencia al subscriber depth
    public int imageWidth = 640;  // Ajusta si tu cámara tiene otro tamaño
    public int imageHeight = 480;

    // Parámetros intrínsecos cámara (ajustar a tu cámara)
    public float fx = 554.254691191187f;
    public float fy = 554.254691191187f;
    public float cx = 320.5f;
    public float cy = 240.5f;

    // Llamar pasando coordenadas pixel en RGB y calcular info mundo
    public void GetPixelWorldInfo(int x, int y)
    {
        if (depthImageSubscriber == null)
        {
            Debug.LogError("No se asignó DepthImageSubscriber");
            return;
        }

        if (x < 0 || x >= imageWidth || y < 0 || y >= imageHeight)
        {
            Debug.LogWarning("Pixel fuera de rango");
            return;
        }

        float depth = depthImageSubscriber.GetDepthAt(x, y);

        if (float.IsNaN(depth))
        {
            Debug.LogWarning($"Profundidad es NaN. Valor real almacenado: {depth}");
            return;
        }

        if (depth <= 0f)
        {
            Debug.LogWarning("Profundidad inválida o no disponible en pixel");
            return;
        }

        // Calcula coordenadas 3D en sistema de cámara
        float X = (x - cx) * depth / fx;
        float Y = (y - cy) * depth / fy;
        float Z = depth;

        // Ajusta signo en Y si tu sistema de coordenadas lo requiere
        Vector3 pointCamera = new Vector3(X, -Y, Z);

        Debug.Log($"Pixel ({X},{Y}) -> Profundidad = {depth:F3} m -> Punto 3D cámara = {pointCamera}");
    }
}


