using UnityEngine;
using UnityEngine.InputSystem;

// Mueve el robot virtual localmente (sin ROS) cuando no hay conexión.
// Útil para el modo offline con mapas guardados.
[RequireComponent(typeof(CharacterController))]
public class RobotLocalController : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset inputActions;
    public string robotMoveActionName = "Move Robot";

    [Header("Velocidad")]
    public float linearSpeed = 0.5f;
    public float angularSpeed = 90f; // grados/seg

    [Header("Activación")]
    public bool offlineMode = true; // activar solo cuando useROS = false

    private InputAction moveAction;
    private CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (inputActions != null)
        {
            moveAction = inputActions.FindAction(robotMoveActionName, true);
            moveAction.Enable();
        }
    }

    void Update()
    {
        if (!offlineMode || moveAction == null) return;

        Vector2 input = moveAction.ReadValue<Vector2>();

        // Rotación (eje horizontal)
        float rotationAmount = input.x * angularSpeed * Time.deltaTime;
        transform.Rotate(0f, rotationAmount, 0f);

        // Movimiento lineal (eje vertical, hacia adelante local)
        Vector3 move = transform.forward * input.y * linearSpeed * Time.deltaTime;
        controller.Move(move);
    }
}