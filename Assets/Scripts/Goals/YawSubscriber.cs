using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Nav;

public class YawSubscriber : UnitySubscriber<Odometry>
{
    public float Yaw { get; private set; }

    protected override void ReceiveMessage(Odometry message)
    {
        var q = message.pose.pose.orientation;
        Yaw = GetYawFromQuaternion(new Quaternion(
            (float)q.x, (float)q.y, (float)q.z, (float)q.w
        ));
    }

    private float GetYawFromQuaternion(Quaternion q)
    {
        return Mathf.Atan2(2f * (q.w * q.z + q.x * q.y), 1f - 2f * (q.y * q.y + q.z * q.z));
    }
}
