using UnityEngine;

public class VREyeFeed : MonoBehaviour
{
    public MeshRenderer rosImageRenderer; // El plano que recibe la textura ROS

    private GameObject leftEyeQuad;
    private GameObject rightEyeQuad;
    private Material eyeMaterial;

    private bool isImmersive = false;

    void Start()
    {
        if (rosImageRenderer == null)
        {
            Debug.LogError("rosImageRenderer no asignado.");
            return;
        }

        eyeMaterial = new Material(Shader.Find("Unlit/Texture"));
        leftEyeQuad = CreateEyeQuad("LeftEyeQuad", new Vector3(-0.03f, 0, 0.15f));
        rightEyeQuad = CreateEyeQuad("RightEyeQuad", new Vector3(0.03f, 0, 0.15f));

        SetQuadsActive(false); // comienza en modo cabina
    }

    void Update()
    {
        if (rosImageRenderer.material.mainTexture != null)
        {
            eyeMaterial.mainTexture = rosImageRenderer.material.mainTexture;
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

    // Este método lo llamará el GameManager
    public void SetImmersiveMode(bool active)
    {
        isImmersive = active;
        SetQuadsActive(active);
    }
}
