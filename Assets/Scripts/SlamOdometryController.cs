using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Std;

public class SlamOdometryController : MonoBehaviour
{
    private RosSocket rosSocket;
    private string serviceId;
    private string resetSlamService = "/reset_slam";

    void Start()
    {
        rosSocket = FindObjectOfType<RosConnector>().RosSocket;
    }

    public void ResetSlamAndOdometry()
    {
        Debug.Log("Llamando al servicio ROS para reiniciar SLAM y odometría...");
        serviceId = rosSocket.CallService<EmptyRequest, EmptyResponse>(
            resetSlamService,
            ServiceResponseHandler,
            new EmptyRequest()
        );
    }

    private void ServiceResponseHandler(EmptyResponse response)
    {
        Debug.Log("Servicio completado: SLAM y odometría reiniciados.");
    }
}
