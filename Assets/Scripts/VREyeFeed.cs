using UnityEngine;
using UnityEngine.XR;

public class VREyeFeed : MonoBehaviour
{
    public MeshRenderer rosImageRenderer; // Arrástrale el MeshRenderer que ya recibe la textura ROS

    private GameObject leftEyeQuad;
    private GameObject rightEyeQuad;

    void Start()
    {
        if (rosImageRenderer == null)
        {
            Debug.LogError("rosImageRenderer no asignado. Debes asignar el MeshRenderer que recibe la textura ROS.");
            return;
        }

        // Crear los quads
        leftEyeQuad = CreateEyeQuad("LeftEyeQuad", new Vector3(-0.03f, 0, 0.15f));
        rightEyeQuad = CreateEyeQuad("RightEyeQuad", new Vector3(0.03f, 0, 0.15f));
    }

    GameObject CreateEyeQuad(string name, Vector3 localPosition)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;

        // Pegar al XR Camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("No se encontró la cámara principal (Camera.main). Asegúrate de que tu XR Rig tiene una cámara con el tag MainCamera.");
            return null;
        }

        quad.transform.SetParent(mainCam.transform);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = new Vector3(1.8f, 1.8f, 1f); // Ajusta a tu gusto según el FOV

        // Usar el mismo material que ya tiene la imagen del robot
        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.material = rosImageRenderer.material;

        return quad;
    }
}
