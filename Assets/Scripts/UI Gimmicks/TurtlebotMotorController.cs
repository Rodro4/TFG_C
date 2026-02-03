using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Std;

public class TurtlebotMotorController : MonoBehaviour
{
    private RosSocket rosSocket;
    public string motorPowerTopic = "/mobile_base/commands/motor_power";
    private string motorPowerPublisherId;

    private float lastToggleTime = 0f;
    private const float toggleCooldown = 0.3f;

    void Start()
    {
        RosConnector connector = FindObjectOfType<RosConnector>();
        if (connector != null)
        {
            rosSocket = connector.RosSocket;

            motorPowerPublisherId = rosSocket.Advertise<Int16>(motorPowerTopic);
            Debug.Log("TurtlebotMotorController: Motor power topic advertised.");
        }
        else
        {
            Debug.LogError("TurtlebotMotorController: RosConnector not found in the scene.");
        }
    }

    // Enables or disables the robot motors by publishing to the motor power topic
    public void ToggleMotors(bool on)
    {
        if (UnityEngine.Time.time - lastToggleTime < toggleCooldown)
            return;

        lastToggleTime = UnityEngine.Time.time;

        if (rosSocket == null || string.IsNullOrEmpty(motorPowerPublisherId))
            return;

        Int16 powerMsg = new Int16 { data = (short)(on ? 1 : 0) };
        rosSocket.Publish(motorPowerPublisherId, powerMsg);

        Debug.Log("TurtlebotMotorController: Motors " + (on ? "ENABLED" : "DISABLED"));
    }
}