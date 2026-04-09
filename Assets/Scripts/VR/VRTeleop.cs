using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

public class VRTeleop : UnityPublisher<Twist>
{
    public enum POVMode
    {
        Cabin,
        Outside,
        FirstPersonRobot,
        ThirdPersonRobot
    }

    [Header("Player Components")]
    public CharacterController playerCharacterController;
    public Behaviour playerLocomotion;

    [Header("XR Origin")]
    public UnityEngine.Transform xrOrigin;
    public UnityEngine.Transform cabinPoint;
    public UnityEngine.Transform outsidePoint;

    [Header("Robot Cameras")]
    public Camera xrMainCamera;
    public Camera robotFirstPersonCamera;
    public Camera robotThirdPersonCamera;

    [Header("Input")]
    public InputActionAsset inputActions;
    public string playerMoveActionName = "Move";
    public string robotMoveActionName = "Move Robot";
    public string switchPovActionName = "Switch POV";

    private InputAction currentMoveAction;
    private InputAction switchPovAction;

    [Header("Speed Settings")]
    public float linearSpeed = 0.8f;
    public float angularSpeed = 1.0f;

    [Header("UI")]
    public Slider linearSlider;
    public Slider angularSlider;
    public Button switchModeButton;

    [Header("Game Mode")]
    public GameModeManager gameModeManager;

    private POVMode currentPov = POVMode.Cabin;

    private Twist message;
    private bool isRobotMode = false;
    private float lastSwitchTime = 0f;
    private const float switchCooldown = 0.3f;

    // Guardar pose de Outside
    private UnityEngine.Vector3 savedOutsidePosition;
    private UnityEngine.Quaternion savedOutsideRotation;
    private bool hasSavedOutsidePose = false;

    protected override void Start()
    {
        base.Start();
        InitializeMessage();

        if (linearSlider != null)
            linearSlider.onValueChanged.AddListener(SetLinearSpeed);

        if (angularSlider != null)
            angularSlider.onValueChanged.AddListener(SetAngularSpeed);

        if (switchModeButton != null)
            switchModeButton.onClick.AddListener(SwitchMode);

        SetupInputActions();
        UpdateModeState();
        UpdatePOV();
    }

    private void SetupInputActions()
    {
        if (inputActions == null)
            return;

        switchPovAction = inputActions.FindAction(switchPovActionName, true);
        switchPovAction.Enable();
        switchPovAction.performed += ctx => CyclePOV();
    }

    private void InitializeMessage()
    {
        message = new Twist
        {
            linear = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(),
            angular = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3()
        };
    }

    private void FixedUpdate()
    {
        if (!isRobotMode || currentMoveAction == null)
            return;

        // Bloqueo por energía
        if (gameModeManager != null && !gameModeManager.RobotCanMove())
        {
            // Enviar velocidad 0 para asegurarnos que se detiene
            message.linear.x = 0;
            message.angular.z = 0;
            Publish(message);
            return;
        }

        Vector2 input = currentMoveAction.ReadValue<Vector2>();

        message.linear.x = input.y * linearSpeed;
        message.angular.z = -input.x * angularSpeed;

        Publish(message);
    }


    private void SwitchMode()
    {
        if (Time.time - lastSwitchTime < switchCooldown)
            return;

        lastSwitchTime = Time.time;
        SetRobotControl(!isRobotMode);
    }

    private void SetRobotControl(bool active)
    {
        isRobotMode = active;

        if (playerLocomotion != null)
            playerLocomotion.enabled = !active;

        if (playerCharacterController != null)
            playerCharacterController.enabled = !active;

        UpdateModeState();
    }

    private void UpdateModeState()
    {
        if (currentMoveAction != null)
            currentMoveAction.Disable();

        string actionName = isRobotMode ? robotMoveActionName : playerMoveActionName;
        currentMoveAction = inputActions.FindAction(actionName, true);
        currentMoveAction.Enable();
    }

    private void CyclePOV()
    {
        // Guardar pose si salimos de Outside
        if (currentPov == POVMode.Outside)
        {
            savedOutsidePosition = xrOrigin.position;
            savedOutsideRotation = xrOrigin.rotation;
            hasSavedOutsidePose = true;
        }

        currentPov = (POVMode)(((int)currentPov + 1) % 4);

        bool robotPOV =
            currentPov == POVMode.FirstPersonRobot ||
            currentPov == POVMode.ThirdPersonRobot;

        SetRobotControl(robotPOV);
        UpdatePOV();

        Debug.Log($"[VRTeleop] POV cambiado a: {currentPov}");
    }

    private void UpdatePOV()
    {
        // Desactivar todas las cámaras
        xrMainCamera.enabled = false;
        robotFirstPersonCamera.enabled = false;
        robotFirstPersonCamera.gameObject.SetActive(false);
        robotThirdPersonCamera.enabled = false;
        robotThirdPersonCamera.gameObject.SetActive(false);


        switch (currentPov)
        {
            case POVMode.Cabin:

                xrOrigin.position = cabinPoint.position;
                xrOrigin.rotation = cabinPoint.rotation;

                xrMainCamera.enabled = true;
                break;

            case POVMode.Outside:

                if (hasSavedOutsidePose)
                {
                    xrOrigin.position = savedOutsidePosition;
                    xrOrigin.rotation = savedOutsideRotation;
                }
                else
                {
                    xrOrigin.position = outsidePoint.position;
                    xrOrigin.rotation = outsidePoint.rotation;
                }

                xrMainCamera.enabled = true;
                break;

            case POVMode.FirstPersonRobot:
                robotFirstPersonCamera.enabled = true;
                robotFirstPersonCamera.gameObject.SetActive(true);
                break;

            case POVMode.ThirdPersonRobot:
                robotThirdPersonCamera.enabled = true;
                robotThirdPersonCamera.gameObject.SetActive(true);
                break;
        }
    }

    public void SetLinearSpeed(float value) => linearSpeed = value;
    public void SetAngularSpeed(float value) => angularSpeed = angularSpeed = value;
}