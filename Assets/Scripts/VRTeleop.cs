using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

public class VRTeleop : UnityPublisher<Twist>
{
    public InputActionProperty moveAction;
    private Twist message;

    [Header("Speed Settings")]
    public float linearSpeed = 0.5f;
    public float angularSpeed = 1.0f;

    [Header("UI Sliders")]
    public Slider linearSlider;
    public Slider angularSlider;


    protected override void Start()
    {
        base.Start();
        InitializeMessage();

        if (linearSlider != null) linearSlider.onValueChanged.AddListener(SetLinearSpeed);
        if (angularSlider != null) angularSlider.onValueChanged.AddListener(SetAngularSpeed);
    }

    private void FixedUpdate()
    {
        UpdateMessageFromInput();
        Publish(message);
    }

    private void InitializeMessage()
    {
        message = new Twist
        {
            linear = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(),
            angular = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3()
        };
    }


    private void UpdateMessageFromInput()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();

        message.linear.x = input.y * linearSpeed;
        message.angular.z = -input.x * angularSpeed; // negativo para girar en dirección esperada

    }

    public void SetLinearSpeed(float value) => linearSpeed = value;
    public void SetAngularSpeed(float value) => angularSpeed = value;
}
