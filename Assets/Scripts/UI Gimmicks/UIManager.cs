using UnityEngine;

public class UIManager : MonoBehaviour
{
    private bool isCabinMode = true;
    private VREyeFeed eyeFeed;

    [Header("Display Screens")]
    public GameObject rgbScreen;
    public GameObject depthScreen;

    private bool showingRGB = true;

    void Start()
    {
        // Find the VREyeFeed component in the scene
        eyeFeed = FindObjectOfType<VREyeFeed>();

        if (eyeFeed == null)
        {
            Debug.LogWarning("UIManager: VREyeFeed component not found in the scene.");
        }

        SetScreenState(rgb: true);
    }

    // Toggles between cabin mode and immersive mode
    public void ToggleMode()
    {
        isCabinMode = !isCabinMode;
        Debug.Log("UIManager: Mode switched to: " + (isCabinMode ? "Cabin" : "Immersive"));

        if (eyeFeed != null)
        {
            eyeFeed.SetImmersiveMode(!isCabinMode);
        }
    }

    // Toggles between showing the RGB feed and the depth feed
    public void ToggleScreen()
    {
        showingRGB = !showingRGB;
        SetScreenState(showingRGB);

        // Update the screen source in VREyeFeed
        if (eyeFeed != null)
            eyeFeed.SetScreenSource(showingRGB);
    }

    // Activates the specified screen and deactivates the other
    private void SetScreenState(bool rgb)
    {
        if (rgbScreen != null) rgbScreen.SetActive(rgb);
        if (depthScreen != null) depthScreen.SetActive(!rgb);
        Debug.Log("UIManager: Current screen: " + (rgb ? "RGB" : "Depth"));
    }

    void Update()
    {
        // Emergency key to toggle back to cabin mode
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMode();
        }
    }
}
