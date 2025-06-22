using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

public class VRTeleop : UnityPublisher<Twist>
{
    [Header("Input Settings")]
    public InputActionProperty moveAction;

    [Header("Speed Settings")]
    public float linearSpeed = 0.5f;
    public float angularSpeed = 1.0f;

    [Header("UI Sliders")]
    public Slider linearSlider;
    public Slider angularSlider;

    private Twist message;

    protected override void Start()
    {
        base.Start();
        InitializeMessage();

        // Hook up UI sliders to update speed dynamically
        if (linearSlider != null) linearSlider.onValueChanged.AddListener(SetLinearSpeed);
        if (angularSlider != null) angularSlider.onValueChanged.AddListener(SetAngularSpeed);
    }

    private void FixedUpdate()
    {
        UpdateMessageFromInput();
        Publish(message);
    }

    // Initialize an empty Twist message
    private void InitializeMessage()
    {
        message = new Twist
        {
            linear = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(),
            angular = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3()
        };
    }

    // Update the ROS Twist message based on VR input
    private void UpdateMessageFromInput()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();

        message.linear.x = input.y * linearSpeed;
        message.angular.z = -input.x * angularSpeed; // Negative to match expected turning direction
    }

    // Called from UI to update linear speed
    public void SetLinearSpeed(float value) => linearSpeed = value;

    // Called from UI to update angular speed
    public void SetAngularSpeed(float value) => angularSpeed = value;
}
