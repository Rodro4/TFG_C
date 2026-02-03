using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

public class VRTeleop : UnityPublisher<Twist>
{
    [Header("Input")]
    public InputActionAsset inputActions;   // XRI Default Input Actions

    [Tooltip("Nombre exacto de la acción para mover al jugador")]
    public string playerMoveActionName = "Move";

    [Tooltip("Nombre exacto de la acción para mover el robot")]
    public string robotMoveActionName = "Move Robot";

    private InputAction currentMoveAction;

    [Header("Speed Settings")]
    public float linearSpeed = 0.8f;
    public float angularSpeed = 1.0f;

    [Header("UI Sliders")]
    public Slider linearSlider;
    public Slider angularSlider;

    [Header("UI Buttons")]
    public Button switchModeButton;

    private Twist message;
    private bool isRobotMode = false;

    private float lastSwitchTime = 0f;
    private const float switchCooldown = 0.3f;

    protected override void Start()
    {
        base.Start();
        InitializeMessage();

        Debug.Log($"[VRTeleop] Start() -> Modo inicial: PLAYER");

        if (linearSlider != null)
            linearSlider.onValueChanged.AddListener(SetLinearSpeed);

        if (angularSlider != null)
            angularSlider.onValueChanged.AddListener(SetAngularSpeed);

        if (switchModeButton != null)
            switchModeButton.onClick.AddListener(SwitchMode);

        UpdateModeState();
    }

    private void FixedUpdate()
    {
        if (!isRobotMode || currentMoveAction == null)
            return;

        Vector2 input = currentMoveAction.ReadValue<Vector2>();

        message.linear.x = input.y * linearSpeed;
        message.angular.z = -input.x * angularSpeed;

        Publish(message);

        if (input != Vector2.zero)
        {
            Debug.Log(
                $"[VRTeleop] Twist -> linear={message.linear.x:F2}, angular={message.angular.z:F2}"
            );
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

    public void SwitchMode()
    {
        if (Time.time - lastSwitchTime < switchCooldown)
            return;

        lastSwitchTime = Time.time;

        isRobotMode = !isRobotMode;
        Debug.Log($"[VRTeleop] Cambio de modo -> {(isRobotMode ? "ROBOT" : "PLAYER")}");
        UpdateModeState();
    }


    private void UpdateModeState()
    {
        if (inputActions == null)
        {
            Debug.LogError("[VRTeleop] InputActionAsset no asignado.");
            return;
        }

        // Desactivar acción anterior
        if (currentMoveAction != null)
            currentMoveAction.Disable();

        string actionName = isRobotMode ? robotMoveActionName : playerMoveActionName;
        currentMoveAction = inputActions.FindAction(actionName, true);

        if (currentMoveAction == null)
        {
            Debug.LogError($"[VRTeleop] No se encontró la acción '{actionName}'.");
            return;
        }

        currentMoveAction.Enable();

        Debug.Log($"[VRTeleop] Acción activa: {currentMoveAction.name} (enabled={currentMoveAction.enabled})");
    }

    public void SetLinearSpeed(float value)
    {
        linearSpeed = value;
    }

    public void SetAngularSpeed(float value)
    {
        angularSpeed = value;
    }
}
