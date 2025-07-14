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
    public float linearSpeed = 0.8f;
    public float angularSpeed = 1.0f;

    [Header("UI Sliders")]
    public Slider linearSlider;
    public Slider angularSlider;

    [Header("Mode Switch Settings")]
    public InputActionAsset inputActions; // Referencia al XRI Default Input Actions
    public Button switchModeButton;       // Botón de la UI para cambiar de modo

    private Twist message;

    private bool isRobotMode = false; // Estado actual del modo

    protected override void Start()
    {
        base.Start();
        InitializeMessage();

        // Hook up UI sliders
        if (linearSlider != null) linearSlider.onValueChanged.AddListener(SetLinearSpeed);
        if (angularSlider != null) angularSlider.onValueChanged.AddListener(SetAngularSpeed);

        // Hook up the mode switch button
        if (switchModeButton != null)
            switchModeButton.onClick.AddListener(SwitchMode);

        UpdateModeState();
    }

    private void FixedUpdate()
    {
        if (isRobotMode) // Solo mover el robot si estamos en modo robot
        {
            UpdateMessageFromInput();
            Publish(message);
        }
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
        message.angular.z = -input.x * angularSpeed;
    }

    public void SetLinearSpeed(float value) => linearSpeed = value;

    public void SetAngularSpeed(float value) => angularSpeed = value;

    // Método para cambiar entre modos
    public void SwitchMode()
    {
        isRobotMode = !isRobotMode;
        UpdateModeState();
    }

    // Activar/desactivar las acciones correctas según el modo
    private void UpdateModeState()
    {
        var playerMove = inputActions.FindAction("Move", true);
        var robotMove = inputActions.FindAction("Move Robot", true);

        if (isRobotMode)
        {
            playerMove.Disable();
            robotMove.Enable();
        }
        else
        {
            robotMove.Disable();
            playerMove.Enable();
        }
    }
}

