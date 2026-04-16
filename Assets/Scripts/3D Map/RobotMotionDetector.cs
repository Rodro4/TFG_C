using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Nav;
using RosSharp.RosBridgeClient.MessageTypes.Std;

public class RobotMotionDetector : MonoBehaviour
{
    public string odomTopic = "/odom";

    public bool isStationary = false;

    public float linearThreshold = 0.02f;
    public float angularThreshold = 0.05f;

    private RosSocket rosSocket;

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private double lastTime;

    private bool first = true;

    void Start()
    {
        var connector = FindObjectOfType<RosConnector>();
        rosSocket = connector.RosSocket;

        rosSocket.Subscribe<Odometry>(odomTopic, OnOdom);
    }

    void OnOdom(Odometry msg)
    {
        Vector3 pos = new Vector3(
            (float)msg.pose.pose.position.x,
            (float)msg.pose.pose.position.y,
            (float)msg.pose.pose.position.z
        );

        Quaternion rot = new Quaternion(
            (float)msg.pose.pose.orientation.x,
            (float)msg.pose.pose.orientation.y,
            (float)msg.pose.pose.orientation.z,
            (float)msg.pose.pose.orientation.w
        );

        double time =
            msg.header.stamp.secs +
            msg.header.stamp.nsecs * 1e-9;

        if (first)
        {
            lastPosition = pos;
            lastRotation = rot;
            lastTime = time;
            first = false;
            return;
        }

        double dt = time - lastTime;

        if (dt <= 0.0001)
            return;

        float linearSpeed = Vector3.Distance(pos, lastPosition) / (float)dt;

        float angularSpeed = Quaternion.Angle(rot, lastRotation) / (float)dt;

        isStationary =
            linearSpeed < linearThreshold &&
            angularSpeed < angularThreshold;

        lastPosition = pos;
        lastRotation = rot;
        lastTime = time;
    }
}