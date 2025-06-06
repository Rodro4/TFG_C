using System.Threading.Tasks;
using UnityEngine;

public class ClickToMapGoal : MonoBehaviour
{
    public PixelPicker pixelPicker;                // Referencia al script que detecta clicks
    public PixelWorldInfo pixelWorldInfo;          // Para convertir píxel a coordenada 3D
    public SimpleGoalPublisher goalPublisher;      // Para enviar el objetivo al robot

    private void OnEnable()
    {
        // Suscribirse al evento de click del PixelPicker
        pixelPicker.OnPixelClicked += HandlePixelClicked;
    }

    private void OnDisable()
    {
        pixelPicker.OnPixelClicked -= HandlePixelClicked;
    }

    private void HandlePixelClicked(int x, int y)
    {
        Vector3? worldPos = pixelWorldInfo.GetWorldCoordinates(x, y);

        if (worldPos.HasValue)
        {
            goalPublisher.PublishGoal(worldPos.Value);
            Debug.Log($"Objetivo enviado al robot: {worldPos.Value}");
        }
        else
        {
            Debug.LogWarning("No se pudo calcular una posición válida.");
        }
    }
}
