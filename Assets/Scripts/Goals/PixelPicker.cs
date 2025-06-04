using UnityEngine;

public class PixelPicker : MonoBehaviour
{
    public Camera cam;                 // Cámara que hace el raycast (normalmente MainCamera)
    public MeshRenderer targetRenderer;  // El mesh renderer que tiene la textura RGB

    public PixelWorldInfo pixelWorldInfo;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))  // Click izquierdo
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // Comprobar que hemos impactado sobre el objeto correcto
                if (hit.collider.gameObject == targetRenderer.gameObject)
                {
                    Vector2 pixelUV = hit.textureCoord;  // UV entre 0 y 1

                    Texture tex = targetRenderer.material.mainTexture;

                    if (tex != null)
                    {
                        int x = Mathf.FloorToInt(pixelUV.x * tex.width);
                        int y = Mathf.FloorToInt(pixelUV.y * tex.height);

                        Debug.Log($"Click en pixel: ({x}, {y}) de la textura con tamaño {tex.width}x{tex.height}");


                        pixelWorldInfo.GetPixelWorldInfo(x, y);
                    }
                    else
                    {
                        Debug.LogWarning("El MeshRenderer no tiene textura asignada.");
                    }
                }
            }
        }
    }
}
