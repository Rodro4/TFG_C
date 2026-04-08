using UnityEngine;

public class VREyeFeed : MonoBehaviour
{
    public MeshRenderer rgbRenderer;
    public MeshRenderer depthRenderer;

    private Material eyeMaterial;

    private bool showingRGB = true;

    void Start()
    {
        if (rgbRenderer == null || depthRenderer == null)
        {
            Debug.LogError("VREyeFeed: RGB and/or Depth MeshRenderers not assigned.");
            return;
        }

        // Use an unlit material to display the video texture clearly
        eyeMaterial = new Material(Shader.Find("Unlit/Texture"));

        SetScreenSource(true); // Start with RGB feed
    }

    void Update()
    {
        Texture currentTexture = GetCurrentTexture();
        if (currentTexture != null)
        {
            eyeMaterial.mainTexture = currentTexture;
        }
    }

    // Select whether to use the RGB or Depth feed
    public void SetScreenSource(bool useRGB)
    {
        showingRGB = useRGB;
    }

    // Gets the current texture to display
    private Texture GetCurrentTexture()
    {
        if (showingRGB)
            return rgbRenderer.material?.mainTexture;
        else
            return depthRenderer.material?.mainTexture;
    }
}
