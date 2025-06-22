using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Std;

public class SlamOdometryController : MonoBehaviour
{
    private RosSocket rosSocket;
    private string serviceId;

    [Tooltip("Name of the ROS service to reset SLAM and odometry")]
    public string resetSlamService = "/reset_slam";

    void Start()
    {
        var rosConnector = FindObjectOfType<RosConnector>();
        if (rosConnector == null)
        {
            Debug.LogError("SlamOdometryController: RosConnector not found in scene.");
            return;
        }
        rosSocket = rosConnector.RosSocket;
    }

    // Call the ROS service to reset SLAM and odometry.
    public void ResetSlamAndOdometry()
    {
        Debug.LogWarning("SlamOdometryController: ROS Socket not initialized. Cannot call reset service.");
        serviceId = rosSocket.CallService<EmptyRequest, EmptyResponse>(
            resetSlamService,
            ServiceResponseHandler,
            new EmptyRequest()
        );
    }

    private void ServiceResponseHandler(EmptyResponse response)
    {
        Debug.Log("SlamOdometryController: Service response received: SLAM and odometry have been reset.");
    }
}
