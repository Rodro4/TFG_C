using UnityEngine;

public class VREyeFeed : MonoBehaviour
{
    public MeshRenderer rgbRenderer;    // Plano RGB
    public MeshRenderer depthRenderer;  // Plano Depth

    private GameObject leftEyeQuad;
    private GameObject rightEyeQuad;
    private Material eyeMaterial;

    private bool isImmersive = false;

    void Start()
    {
        if (rgbRenderer == null || depthRenderer == null)
        {
            Debug.LogError("No se han asignado los MeshRenderers RGB y/o Depth.");
            return;
        }

        eyeMaterial = new Material(Shader.Find("Unlit/Texture"));

        leftEyeQuad = CreateEyeQuad("LeftEyeQuad", new Vector3(-0.03f, 0, 0.15f));
        rightEyeQuad = CreateEyeQuad("RightEyeQuad", new Vector3(0.03f, 0, 0.15f));

        SetQuadsActive(false); // comienza en modo cabina
        SetScreenSource(true); // comienza en modo RGB
    }

    void Update()
    {
        Texture currentTexture = GetCurrentTexture();
        if (currentTexture != null)
        {
            eyeMaterial.mainTexture = currentTexture;
        }
    }

    GameObject CreateEyeQuad(string name, Vector3 localPosition)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("No se encontró la cámara principal.");
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

    private void SetQuadsActive(bool active)
    {
        if (leftEyeQuad != null) leftEyeQuad.SetActive(active);
        if (rightEyeQuad != null) rightEyeQuad.SetActive(active);
    }

    public void SetImmersiveMode(bool active)
    {
        isImmersive = active;
        SetQuadsActive(active);
    }

    private bool showingRGB = true;
    public void SetScreenSource(bool useRGB)
    {
        showingRGB = useRGB;
    }

    private Texture GetCurrentTexture()
    {
        if (showingRGB)
            return rgbRenderer.material?.mainTexture;
        else
            return depthRenderer.material?.mainTexture;
    }
}
