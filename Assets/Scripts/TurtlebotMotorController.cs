using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Std;

public class TurtlebotMotorController : MonoBehaviour
{
    private RosSocket rosSocket;
    public string motorPowerTopic = "/mobile_base/commands/motor_power";
    private string motorPowerPublisherId;

    void Start()
    {
        RosConnector connector = FindObjectOfType<RosConnector>();
        if (connector != null)
        {
            rosSocket = connector.RosSocket;
            motorPowerPublisherId = rosSocket.Advertise<Int16>(motorPowerTopic);
            Debug.Log("Topic de motor power anunciado.");
        }
        else
        {
            Debug.LogError("RosConnector no encontrado en la escena.");
        }
    }

    public void ToggleMotors(bool on)
    {
        if (rosSocket == null || string.IsNullOrEmpty(motorPowerPublisherId)) return;

        Int16 powerMsg = new Int16 { data = (short)(on ? 1 : 0) };
        rosSocket.Publish(motorPowerPublisherId, powerMsg);
        Debug.Log("Motores " + (on ? "ENCENDIDOS" : "APAGADOS"));
    }
}
