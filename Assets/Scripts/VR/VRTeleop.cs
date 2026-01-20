//using UnityEngine;
//using UnityEngine.UI;
//using UnityEngine.InputSystem;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Geometry;

//public class VRTeleop : UnityPublisher<Twist>
//{
//    [Header("Input Settings")]
//    public InputActionProperty moveAction;

//    [Header("Speed Settings")]
//    public float linearSpeed = 0.8f;
//    public float angularSpeed = 1.0f;

//    [Header("UI Sliders")]
//    public Slider linearSlider;
//    public Slider angularSlider;

//    [Header("Mode Switch Settings")]
//    public InputActionAsset inputActions; // Referencia al XRI Default Input Actions
//    public Button switchModeButton;       // Botón de la UI para cambiar de modo

//    private Twist message;

//    private bool isRobotMode = false; // Estado actual del modo

//    protected override void Start()
//    {
//        base.Start();
//        InitializeMessage();

//        // Inicio
//        Debug.Log($"[VRTeleop] Start() -> Inicializando. Modo inicial: {(isRobotMode ? "ROBOT" : "PLAYER")}");

//        // Hook up UI sliders
//        if (linearSlider != null)
//        {
//            linearSlider.onValueChanged.AddListener(SetLinearSpeed);
//            Debug.Log("[VRTeleop] Linear slider conectado correctamente.");
//        }
//        else Debug.LogWarning("[VRTeleop]  Linear slider no asignado.");

//        if (angularSlider != null)
//        {
//            angularSlider.onValueChanged.AddListener(SetAngularSpeed);
//            Debug.Log("[VRTeleop] Angular slider conectado correctamente.");
//        }
//        else Debug.LogWarning("[VRTeleop]  Angular slider no asignado.");

//        // Hook up the mode switch button
//        if (switchModeButton != null)
//        {
//            switchModeButton.onClick.AddListener(SwitchMode);
//            Debug.Log("[VRTeleop] Botón de cambio de modo (UI) conectado correctamente.");
//        }
//        else Debug.LogWarning("[VRTeleop]  No se ha asignado el botón de cambio de modo (UI).");

//        UpdateModeState();
//    }

//    private void FixedUpdate()
//    {
//        if (isRobotMode) // Solo mover el robot si estamos en modo robot
//        {
//            UpdateMessageFromInput();
//            Publish(message);

//            // Verificar publicación de mensajes Twist
//            Debug.Log($"[VRTeleop] Publicando Twist -> linear.x={message.linear.x:F2}, angular.z={message.angular.z:F2}");    
//        }
//    }

//    private void InitializeMessage()
//    {
//        message = new Twist
//        {
//            linear = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(),
//            angular = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3()
//        };
//        Debug.Log("[VRTeleop] Twist message inicializado correctamente.");
//    }

//    private void UpdateMessageFromInput()
//    {
//        if (moveAction == null || moveAction.action == null)
//        {
//            Debug.LogWarning("[VRTeleop]  moveAction no asignado o sin acción válida.");
//            return;
//        }

//        Vector2 input = moveAction.action.ReadValue<Vector2>();

//        // Mostrar el valor del joystick si hay movimiento
//        if (input != Vector2.zero)
//            Debug.Log($"[VRTeleop] Input joystick detectado -> X={input.x:F2}, Y={input.y:F2}");

//        message.linear.x = input.y * linearSpeed;
//        message.angular.z = -input.x * angularSpeed;
//    }

//    public void SetLinearSpeed(float value)
//    {
//        linearSpeed = value;
//        Debug.Log($"[VRTeleop] LinearSpeed actualizado -> {linearSpeed:F2}");
//    }

//    public void SetAngularSpeed(float value)
//    {
//        angularSpeed = value;
//        Debug.Log($"[VRTeleop] AngularSpeed actualizado -> {angularSpeed:F2}");
//    }

//    // Método para cambiar entre modos
//    public void SwitchMode()
//    {
//        isRobotMode = !isRobotMode;
//        Debug.Log($"[VRTeleop] Cambio de modo solicitado. Nuevo modo: {(isRobotMode ? "ROBOT" : "PLAYER")}");
//        UpdateModeState();
//    }

//    // Activar/desactivar las acciones correctas según el modo
//    private void UpdateModeState()
//    {
//        // Comprobar que el asset está asignado
//        if (inputActions == null)
//        {
//            Debug.LogError("[VRTeleop]  InputActionAsset no asignado. No se pueden activar/desactivar acciones.");
//            return;
//        }

//        var playerMove = inputActions.FindAction("Move", false);
//        var robotMove = inputActions.FindAction("Move Robot", false);

//        if (playerMove == null)
//            Debug.LogWarning("[VRTeleop]  No se ha encontrado la acción 'Move' en el asset.");
//        if (robotMove == null)
//            Debug.LogWarning("[VRTeleop]  No se ha encontrado la acción 'Move Robot' en el asset.");

//        if (isRobotMode)
//        {
//            if (playerMove != null) playerMove.Disable();
//            if (robotMove != null) robotMove.Enable();
//        }
//        else
//        {
//            if (robotMove != null) robotMove.Disable();
//            if (playerMove != null) playerMove.Enable();
//        }

//        // Resumen tras aplicar cambios
//        Debug.Log($"[VRTeleop] UpdateModeState() -> Modo actual: {(isRobotMode ? "ROBOT" : "PLAYER")}");
//        Debug.Log($"[VRTeleop]    PlayerMove.enabled={(playerMove != null ? playerMove.enabled : false)}");
//        Debug.Log($"[VRTeleop]    RobotMove.enabled={(robotMove != null ? robotMove.enabled : false)}");

//        if (moveAction != null && moveAction.action != null)
//            Debug.Log($"[VRTeleop]    moveAction actualmente asignada a: '{moveAction.action.name}'");
//        else
//            Debug.LogWarning("[VRTeleop]  moveAction.action es nula o no está asignada.");
//    }
//}


















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
