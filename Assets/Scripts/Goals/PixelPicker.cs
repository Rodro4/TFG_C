using RosSharp.RosBridgeClient;
using UnityEngine;
using UnityEngine.InputSystem;

public class PixelPicker : MonoBehaviour
{
    public Camera cam;                      // Camera for raycasting
    public MeshRenderer targetRenderer;    // Renderer holding the RGB texture

    public delegate void PixelClickAction(int x, int y);
    public event PixelClickAction OnPixelClicked;

    public DepthImageSubscriber depthImageSubscriber; // To get depth info

    // Image size matching the depth and RGB streams (adjust as needed)
    public int imageWidth = 640;
    public int imageHeight = 480;

    // Camera intrinsics (focal length and principal point)
    public float fx = 554.254691191187f;
    public float fy = 554.254691191187f;
    public float cx = 320.5f;
    public float cy = 240.5f;

    public InputActionProperty vrSelectAction;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))  // On left click
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == targetRenderer.gameObject)
                {
                    Vector2 pixelUV = hit.textureCoord; // UV coordinates [0..1]

                    Texture tex = targetRenderer.material.mainTexture;

                    if (tex != null)
                    {
                        int x = Mathf.FloorToInt(pixelUV.x * tex.width);
                        int y = Mathf.FloorToInt((1.0f - pixelUV.y) * tex.height); // Flip Y axis

                        Debug.Log($"PixelPicker: Clicked pixel ({x}, {y}) on texture {tex.width}x{tex.height}");

                        OnPixelClicked?.Invoke(x, y);
                    }
                    else
                    {
                        Debug.LogWarning("PixelPicker: MeshRenderer has no texture assigned.");
                    }
                }
            }
        }

        if (vrSelectAction.action != null && vrSelectAction.action.WasPressedThisFrame())
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == targetRenderer.gameObject)
                {
                    Vector2 pixelUV = hit.textureCoord;

                    Texture tex = targetRenderer.material.mainTexture;

                    if (tex != null)
                    {
                        int x = Mathf.FloorToInt(pixelUV.x * tex.width);
                        int y = Mathf.FloorToInt((1.0f - pixelUV.y) * tex.height);

                        Debug.Log($"PixelPicker (VR): Selected pixel ({x}, {y})");
                        OnPixelClicked?.Invoke(x, y);
                    }
                    else
                    {
                        Debug.LogWarning("PixelPicker (VR): No texture assigned.");
                    }
                }
            }
        }
    }

    // Converts pixel coordinates plus depth to a 3D world point. Returns null if depth data unavailable or invalid
    public Vector3? GetWorldCoordinates(int x, int y)
    {
        if (depthImageSubscriber == null)
            return null;

        if (x < 0 || x >= imageWidth || y < 0 || y >= imageHeight)
            return null;

        float depth = depthImageSubscriber.GetDepthAt(x, y);

        if (float.IsNaN(depth) || depth <= 0f)
            return null;

        // Convert pixel + depth to camera space coordinates
        float X = (x - cx) * depth / fx;
        float Y = (y - cy) * depth / fy;
        float Z = depth;

        Vector3 pointCamera = new Vector3(X, -Y, Z); // Y flipped for Unity coordinates

        // Convert from local camera space to world space
        Vector3 worldPos = transform.TransformPoint(pointCamera);

        return worldPos;
    }
}
