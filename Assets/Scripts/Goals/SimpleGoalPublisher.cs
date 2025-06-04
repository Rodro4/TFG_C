using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Std;

public class SimpleGoalPublisher : UnityPublisher<PoseStamped>
{
    public string FrameId = "map";
    private PoseStamped message;

    protected override void Start()
    {
        base.Start();
        InitializeMessage();
        PublishExactPose();
    }

    private void InitializeMessage()
    {
        message = new PoseStamped
        {
            header = new Header { frame_id = FrameId },
            pose = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose()
        };
    }

    private void PublishExactPose()
    {
        message.header.Update();

        // Posición exacta
        message.pose.position = new Point
        {
            x = -0.759414434433,
            y = -0.254543423653,
            z = 0.0
        };

        // Orientación exacta
        message.pose.orientation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion
        {
            x = 0.0,
            y = 0.0,
            z = 0.938722214881,
            w = -0.344674633951
        };

        Publish(message);
        Debug.Log("Mensaje exacto publicado.");
    }

    public void PublishGoal(UnityEngine.Vector3 position)
    {
        message.header.Update();
        message.pose.position = new Point
        {
            x = position.x,
            y = position.y,
            z = 0.0  // siempre en el plano 2D
        };


        // Asumimos orientación plana hacia adelante
        message.pose.orientation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion
        {
            x = 0.0,
            y = 0.0,
            z = 0.0,
            w = 1.0
        };

        Publish(message);
        Debug.Log($"Objetivo publicado en /map: {position}");
    }
}