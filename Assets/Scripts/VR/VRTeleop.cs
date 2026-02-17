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

    [Header("Locomotion")]
    public Behaviour playerLocomotion;

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

    [Header("POV System")]
    public UnityEngine.Transform xrOrigin;
    public UnityEngine.Transform cabinPoint;
    public UnityEngine.Transform outsidePoint;
    public UnityEngine.Transform robotURDF;
    public UnityEngine.Vector3 thirdPersonOffset = new UnityEngine.Vector3(0, 2f, -3f);

    private POVMode currentPov = POVMode.Cabin;

    private Twist message;
    private bool isRobotMode = false;
    private float lastSwitchTime = 0f;
    private const float switchCooldown = 0.3f;

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
        //switchPovAction.performed += ctx => CyclePOV();
        switchPovAction.performed += ctx =>
        {
            Debug.Log("Switch POV pressed");
            CyclePOV();
        };

    }

    private void FixedUpdate()
    {
        if (!isRobotMode || currentMoveAction == null)
            return;

        Vector2 input = currentMoveAction.ReadValue<Vector2>();

        message.linear.x = input.y * linearSpeed;
        message.angular.z = -input.x * angularSpeed;

        Publish(message);
    }

    private void Update()
    {
        if (currentPov == POVMode.FirstPersonRobot && robotURDF != null)
        {
            xrOrigin.position = robotURDF.position;
            xrOrigin.rotation = robotURDF.rotation;
        }

        if (currentPov == POVMode.ThirdPersonRobot && robotURDF != null)
        {
            UnityEngine.Vector3 desiredPos = robotURDF.TransformPoint(thirdPersonOffset);
            xrOrigin.position = desiredPos;
            xrOrigin.LookAt(robotURDF.position + UnityEngine.Vector3.up * 0.5f);
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

        if (playerLocomotion != null)
            playerLocomotion.enabled = !isRobotMode;

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
        currentPov = (POVMode)(((int)currentPov + 1) % 4);
        UpdatePOV();
        Debug.Log($"[VRTeleop] POV cambiado a: {currentPov}");
    }

    private void UpdatePOV()
    {
        if (xrOrigin == null)
            return;

        switch (currentPov)
        {
            case POVMode.Cabin:
                xrOrigin.position = cabinPoint.position;
                xrOrigin.rotation = cabinPoint.rotation;
                break;

            case POVMode.Outside:
                xrOrigin.position = outsidePoint.position;
                xrOrigin.rotation = outsidePoint.rotation;
                break;

            case POVMode.FirstPersonRobot:
                // Se actualiza continuamente en Update()
                break;

            case POVMode.ThirdPersonRobot:
                // Se actualiza continuamente en Update()
                break;
        }
    }

    public void SetLinearSpeed(float value) => linearSpeed = value;
    public void SetAngularSpeed(float value) => angularSpeed = value;
}
