using UnityEngine;

public class VREyeFeed : MonoBehaviour
{
    public MeshRenderer rgbRenderer;
    public MeshRenderer depthRenderer;

    private GameObject leftEyeQuad;
    private GameObject rightEyeQuad;
    private Material eyeMaterial;

    private bool isImmersive = false;
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

        leftEyeQuad = CreateEyeQuad("LeftEyeQuad", new Vector3(-0.03f, 0, 0.15f));
        rightEyeQuad = CreateEyeQuad("RightEyeQuad", new Vector3(0.03f, 0, 0.15f));

        SetQuadsActive(false); // Start in cockpit mode
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

    // Creates a textured quad in front of the VR camera
    GameObject CreateEyeQuad(string name, Vector3 localPosition)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("VREyeFeed: Main camera not found.");
            return null;
        }

        quad.transform.SetParent(mainCam.transform);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.material = eyeMaterial;

        return quad;
    }

    // Enables or disables the eye quads
    private void SetQuadsActive(bool active)
    {
        if (leftEyeQuad != null) leftEyeQuad.SetActive(active);
        if (rightEyeQuad != null) rightEyeQuad.SetActive(active);
    }

    // Toggles immersive mode (quads enabled/disabled)
    public void SetImmersiveMode(bool active)
    {
        isImmersive = active;
        SetQuadsActive(active);
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
