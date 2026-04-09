using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

public class SimpleTeleop : UnityPublisher<Twist>
{
    public float forwardSpeed = 0.8f;

    private Twist message;
    private bool isMoving = false;

    protected override void Start()
    {
        base.Start();

        message = new Twist
        {
            linear = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(),
            angular = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3()
        };
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            isMoving = !isMoving;
            Debug.Log("Movimiento toggle: " + (isMoving ? "ON" : "OFF"));
        }

        if (isMoving)
        {
            message.linear.x = forwardSpeed;
        }
        else
        {
            message.linear.x = 0;
        }

        message.angular.z = 0;

        Publish(message);
    }
}