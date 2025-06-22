using UnityEngine;
using System.Collections;

// Handles click-based relative navigation by rotating the robot toward a point and then moving forward. Uses Twist messages and odometry yaw data
public class ClickToRelativeMove : MonoBehaviour
{
    [Header("Dependencies")]
    public PixelPicker pixelPicker;
    public SimpleTwistPublisher twistPublisher;
    public YawSubscriber yawSubscriber;

    [Header("Movement Parameters")]
    public float linearSpeed = 0.2f;
    public float angularSpeed = 0.5f;
    public float angularThresholdDeg = 2f;
    public float stopDelay = 0.1f;
    public float angularKp = 2.0f;

    private void Start()
    {
        pixelPicker.OnPixelClicked += OnPixelClickedHandler;
    }

    private void OnDestroy()
    {
        pixelPicker.OnPixelClicked -= OnPixelClickedHandler;
    }

    // Triggered when a pixel is clicked on screen. Converts the pixel to a 3D point and calculates a rotation + forward move
    private void OnPixelClickedHandler(int x, int y)
    {
        UnityEngine.Vector3? point = pixelPicker.GetWorldCoordinates(x, y);
        if (!point.HasValue) return;

        UnityEngine.Vector3 camPoint = point.Value;
        float forwardDistance = camPoint.z;
        float lateralOffset = camPoint.x;

        float relativeAngleRad = -Mathf.Atan2(lateralOffset, forwardDistance);
        float forwardDuration = forwardDistance / linearSpeed;

        StartCoroutine(RotateThenMove(relativeAngleRad, forwardDuration));
    }

    // Performs two-step navigation: first rotation, then linear forward motion.
    private IEnumerator RotateThenMove(float relativeAngleRad, float forwardDuration)
    {
        // --- PHASE 1: Rotation ---
        float initialYaw = yawSubscriber.Yaw;
        float targetYaw = NormalizeAngleRad(initialYaw + relativeAngleRad);

        while (true)
        {
            float currentYaw = yawSubscriber.Yaw;
            float error = NormalizeAngleRad(targetYaw - currentYaw);

            if (Mathf.Abs(error) < angularThresholdDeg * Mathf.Deg2Rad)
                break;

            float angularZ = Mathf.Clamp(error * angularKp, -angularSpeed, angularSpeed);
            twistPublisher.PublishVelocity(0, angularZ);
            yield return null;
        }

        twistPublisher.Stop();
        yield return new WaitForSeconds(stopDelay);

        // --- PHASE 2: Forward movement ---
        float elapsed = 0f;
        while (elapsed < forwardDuration)
        {
            twistPublisher.PublishVelocity(linearSpeed, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        twistPublisher.Stop();
    }

    // Normalizes an angle to the range [-Pi, Pi].
    private float NormalizeAngleRad(float angle)
    {
        while (angle > Mathf.PI) angle -= 2 * Mathf.PI;
        while (angle < -Mathf.PI) angle += 2 * Mathf.PI;
        return angle;
    }
}
