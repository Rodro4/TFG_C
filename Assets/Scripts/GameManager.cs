using UnityEngine;

public class GameManager : MonoBehaviour
{
    private bool isCabinMode = true;
    private VREyeFeed eyeFeed;

    [Header("Pantallas de visualización")]
    public GameObject rgbScreen;
    public GameObject depthScreen;

    private bool showingRGB = true;

    void Start()
    {
        eyeFeed = FindObjectOfType<VREyeFeed>();

        if (eyeFeed == null)
        {
            Debug.LogWarning("VREyeFeed no encontrado en la escena.");
        }

        SetScreenState(rgb: true);
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

    public void ToggleScreen()
    {
        showingRGB = !showingRGB;
        SetScreenState(showingRGB);

        if (eyeFeed != null)
            eyeFeed.SetScreenSource(showingRGB);
    }

    private void SetScreenState(bool rgb)
    {
        if (rgbScreen != null) rgbScreen.SetActive(rgb);
        if (depthScreen != null) depthScreen.SetActive(!rgb);
        Debug.Log("Pantalla actual: " + (rgb ? "RGB" : "Depth"));
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
