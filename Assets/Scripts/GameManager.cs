using UnityEngine;

public class GameManager : MonoBehaviour
{
    private bool isCabinMode = true;
    private VREyeFeed eyeFeed;

    void Start()
    {
        eyeFeed = FindObjectOfType<VREyeFeed>();

        if (eyeFeed == null)
        {
            Debug.LogWarning("VREyeFeed no encontrado en la escena.");
        }
    }

    public void ToggleMode()
    {
        isCabinMode = !isCabinMode;
        Debug.Log("Modo cambiado a: " + (isCabinMode ? "Cabina" : "Inmersivo"));

        if (eyeFeed != null)
        {
            eyeFeed.SetImmersiveMode(!isCabinMode);
        }
    }

    void Update()
    {
        // Botón de emergencia para volver al modo cabina
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMode();
        }
    }
}
