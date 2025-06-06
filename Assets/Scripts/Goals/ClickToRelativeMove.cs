//using UnityEngine;
//using System.Collections;

//public class ClickToRelativeMove : MonoBehaviour
//{
//    public PixelPicker pixelPicker;
//    public PixelWorldInfo pixelWorldInfo;
//    public SimpleTwistPublisher twistPublisher;

//    [Header("Parámetros de movimiento")]
//    public float linearSpeed = 0.2f;
//    public float angularSpeed = 0.5f;
//    public float angularThresholdDeg = 2f;
//    public float stopDelay = 0.1f;
//    public float angularKp = 2.0f;

//    private void Start()
//    {
//        pixelPicker.OnPixelClicked += OnPixelClickedHandler;
//    }

//    private void OnDestroy()
//    {
//        pixelPicker.OnPixelClicked -= OnPixelClickedHandler;
//    }

//    private void OnPixelClickedHandler(int x, int y)
//    {
//        UnityEngine.Vector3? point = pixelWorldInfo.GetWorldCoordinates(x, y);
//        if (!point.HasValue) return;

//        UnityEngine.Vector3 camPoint = point.Value;
//        float forwardDistance = camPoint.z;
//        float lateralOffset = camPoint.x;

//        // Ángulo relativo al robot
//        float relativeAngleRad = Mathf.Atan2(lateralOffset, forwardDistance);
//        float forwardDuration = forwardDistance / linearSpeed;

//        Debug.Log($"[MOV] Girar {relativeAngleRad * Mathf.Rad2Deg:F1}° y avanzar {forwardDistance:F2} m");

//        StartCoroutine(RotateThenMove(relativeAngleRad, forwardDuration));
//    }

//    private IEnumerator RotateThenMove(float relativeAngleRad, float forwardDuration)
//    {
//        // Fase 1: Gira hasta estar bien orientado
//        Debug.Log("[Fase 1] Girando");
//        while (Mathf.Abs(relativeAngleRad) > angularThresholdDeg * Mathf.Deg2Rad)
//        {
//            float angularZ = Mathf.Clamp(relativeAngleRad * angularKp, -angularSpeed, angularSpeed);
//            twistPublisher.PublishVelocity(0, angularZ);
//            yield return null;

//            relativeAngleRad -= angularZ * Time.deltaTime;
//        }

//        twistPublisher.Stop();
//        yield return new WaitForSeconds(stopDelay);

//        // Fase 2: Avanzar recto
//        Debug.Log("[Fase 2] Avanzando recto");
//        float elapsed = 0f;
//        while (elapsed < forwardDuration)
//        {
//            twistPublisher.PublishVelocity(linearSpeed, 0);
//            elapsed += Time.deltaTime;
//            yield return null;
//        }

//        twistPublisher.Stop();
//        Debug.Log("[FIN] Movimiento completado");
//    }
//}










using UnityEngine;
using System.Collections;

public class ClickToRelativeMove : MonoBehaviour
{
    public PixelPicker pixelPicker;
    public PixelWorldInfo pixelWorldInfo;
    public SimpleTwistPublisher twistPublisher;
    public YawSubscriber yawSubscriber;

    [Header("Parámetros de movimiento")]
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

    private void OnPixelClickedHandler(int x, int y)
    {
        UnityEngine.Vector3? point = pixelWorldInfo.GetWorldCoordinates(x, y);
        if (!point.HasValue) return;

        UnityEngine.Vector3 camPoint = point.Value;
        float forwardDistance = camPoint.z;
        float lateralOffset = camPoint.x;

        // CORREGIDO: Signo para que izquierda sea izquierda
        float relativeAngleRad = -Mathf.Atan2(lateralOffset, forwardDistance);
        float forwardDuration = forwardDistance / linearSpeed;

        Debug.Log($"[MOV] Girar {relativeAngleRad * Mathf.Rad2Deg:F1}° y avanzar {forwardDistance:F2} m");

        StartCoroutine(RotateThenMove(relativeAngleRad, forwardDuration));
    }

    private IEnumerator RotateThenMove(float relativeAngleRad, float forwardDuration)
    {
        // FASE 1: Gira hasta tener el ángulo relativo corregido
        Debug.Log("[Fase 1] Girando");
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

        // FASE 2: Avanzar recto
        Debug.Log("[Fase 2] Avanzando recto");
        float elapsed = 0f;
        while (elapsed < forwardDuration)
        {
            twistPublisher.PublishVelocity(linearSpeed, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        twistPublisher.Stop();
        Debug.Log("[FIN] Movimiento completado");
    }

    private float NormalizeAngleRad(float angle)
    {
        while (angle > Mathf.PI) angle -= 2 * Mathf.PI;
        while (angle < -Mathf.PI) angle += 2 * Mathf.PI;
        return angle;
    }
}
