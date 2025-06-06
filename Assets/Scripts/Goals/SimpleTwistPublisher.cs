using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using MsgVector3 = RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3;

public class SimpleTwistPublisher : UnityPublisher<Twist>
{
    private Twist message;

    protected override void Start()
    {
        base.Start();
        message = new Twist
        {
            linear = new MsgVector3(),
            angular = new MsgVector3()
        };
    }

    public void PublishVelocity(double linearX, double angularZ)
    {
        message.linear.x = linearX;
        message.linear.y = 0;
        message.linear.z = 0;

        message.angular.x = 0;
        message.angular.y = 0;
        message.angular.z = angularZ;

        Publish(message);
    }

    public void Stop()
    {
        PublishVelocity(0.0, 0.0);
    }
}
