using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Std;

/// <summary>
/// Panel de control centralizado del Turtlebot2.
/// Conecta con turtlebot2_robot_manager.py a través de rosbridge.
///
/// Servicios que llama:
///   /reset_slam       (std_srvs/Empty)
///   /reset_odometry   (std_srvs/Empty)
///   /set_motors       (std_srvs/SetBool)
///
/// Topics suscritos:
///   /robot_status     (std_msgs/String → JSON)
/// </summary>
public class RobotPanelManager : MonoBehaviour
{
    // ─── Referencias UI ──────────────────────────────────────────────
    [Header("Botones")]
    public Button resetSlamButton;
    public Button resetOdometryButton;
    public Button motorsToggleButton;

    [Header("Textos de estado")]
    public TMP_Text batteryText;      // "Batería: 78.3%  (15.4V)"
    public TMP_Text speedText;        // "Lin: 0.12 m/s  Ang: 0.00 rad/s"
    public TMP_Text motorStatusText;  // "Motores: ON"
    public TMP_Text rosStatusText;    // "ROS: Conectado / Desconectado"

    [Header("Cooldown entre llamadas (seg)")]
    public float cooldown = 1.0f;

    // ─── Internos ────────────────────────────────────────────────────
    private RosSocket rosSocket;
    private float lastCallTime = -999f;
    private bool rosConnected  = false;

    // Estado recibido del robot
    private float batteryPct    = -1f;
    private float batteryV      = 0f;
    private float linearSpeed   = 0f;
    private float angularSpeed  = 0f;
    private bool motorsEnabled = true;
    private string motorPublisherId;

    // Flag thread-safe: el suscriptor corre en hilo secundario
    private volatile bool statusDirty = false;
    private readonly object statusLock = new object();

    // ─── Unity lifecycle ─────────────────────────────────────────────
    void Start()
    {
        var connector = FindObjectOfType<RosConnector>();
        if (connector == null)
        {
            Debug.LogError("[RobotPanelManager] RosConnector no encontrado.");
            SetRosStatus(false);
            return;
        }

        rosSocket    = connector.RosSocket;
        rosConnected = true;
        SetRosStatus(true);

        // Suscripción al estado del robot
        rosSocket.Subscribe<RosSharp.RosBridgeClient.MessageTypes.Std.String>(
            "/robot_status", OnStatusReceived);

        motorPublisherId = rosSocket.Advertise<RosSharp.RosBridgeClient.MessageTypes.Std.Int16>("/mobile_base/commands/motor_power");

        // Botones
        resetSlamButton    ?.onClick.AddListener(ResetSlam);
        resetOdometryButton?.onClick.AddListener(ResetOdometry);
        motorsToggleButton?.onClick.AddListener(ToggleMotors);

        UpdateUI();
    }

    void Update()
    {
        if (statusDirty)
        {
            statusDirty = false;
            UpdateUI();
        }
    }

    // ─── Callback ROS (hilo secundario) ──────────────────────────────
    private void OnStatusReceived(RosSharp.RosBridgeClient.MessageTypes.Std.String msg)
    {
        try
        {
            var data = JsonUtility.FromJson<RobotStatus>(msg.data);
            lock (statusLock)
            {
                batteryPct    = data.battery_pct;
                batteryV      = data.battery_v;
                linearSpeed   = data.linear_speed;
                angularSpeed  = data.angular_speed;
            }
            statusDirty = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[RobotPanelManager] Error parseando status: " + e.Message);
        }
    }

    // ─── Actualizar UI (hilo principal) ──────────────────────────────
    private void UpdateUI()
    {
        float bPct, bV, linSpd, angSpd;
        bool  mOn;

        lock (statusLock)
        {
            bPct   = batteryPct;
            bV     = batteryV;
            linSpd = linearSpeed;
            angSpd = angularSpeed;
            mOn    = motorsEnabled;
        }

        if (batteryText != null)
        {
            if (bPct < 0)
                batteryText.text = "Batería: --";
            else
            {
                // Color según nivel
                string color = bPct > 50f ? "green" : bPct > 20f ? "yellow" : "red";
                batteryText.text = $"<color={color}>Batería: {bPct:F1}%  ({bV:F2} V)</color>";
            }
        }

        if (speedText != null)
            speedText.text = $"Lin: {linSpd:F2} m/s   Ang: {angSpd:F2} rad/s";

        if (motorStatusText != null)
        {
            string color = mOn ? "green" : "red";
            motorStatusText.text = $"Motores: <color={color}>{(mOn ? "ON" : "OFF")}</color>";
        }
    }

    private void SetRosStatus(bool connected)
    {
        rosConnected = connected;
        if (rosStatusText != null)
        {
            rosStatusText.text = connected
                ? "<color=green>ROS: Conectado</color>"
                : "<color=red>ROS: Desconectado</color>";
        }
    }

    // ─── Cooldown helper ─────────────────────────────────────────────
    private bool CanCall()
    {
        if (UnityEngine.Time.time - lastCallTime < cooldown) return false;
        if (!rosConnected) { Debug.LogWarning("[RobotPanelManager] ROS no conectado."); return false; }
        lastCallTime = UnityEngine.Time.time;
        return true;
    }

    // ─── Acciones ────────────────────────────────────────────────────

    /// <summary>Resetea gmapping (SLAM) + odometría del hardware.</summary>
    public void ResetSlam()
    {
        if (!CanCall()) return;
        rosSocket.CallService<EmptyRequest, EmptyResponse>(
            "/reset_slam", _ => Debug.Log("[RobotPanelManager] SLAM reseteado."),
            new EmptyRequest());
    }

    /// <summary>Solo resetea la odometría, sin tocar el mapa SLAM.</summary>
    public void ResetOdometry()
    {
        if (!CanCall()) return;
        rosSocket.CallService<EmptyRequest, EmptyResponse>(
            "/reset_odometry", _ => Debug.Log("[RobotPanelManager] Odometría reseteada."),
            new EmptyRequest());
    }

    /// <summary>Alterna el estado de los motores.</summary>
    public void ToggleMotors()
    {
        if (!CanCall()) return;
        CallSetMotors(!motorsEnabled);
    }

    private void CallSetMotors(bool enable)
    {
        if (string.IsNullOrEmpty(motorPublisherId)) return;
        var msg = new RosSharp.RosBridgeClient.MessageTypes.Std.Int16 { data = (short)(enable ? 1 : 0) };
        rosSocket.Publish(motorPublisherId, msg);
        // Actualizar estado local inmediatamente para que el toggle funcione
        lock (statusLock) { motorsEnabled = enable; }
        UpdateUI();
    }

    // ─── JSON helper ─────────────────────────────────────────────────
    [Serializable]
    private class RobotStatus
    {
        public float battery_v;
        public float battery_pct;
        public bool  motors_enabled;
        public float linear_speed;
        public float angular_speed;
    }

    // ─── Clases mínimas para SetBool si RosSharp no las incluye ──────
    [Serializable]
    public class SetBoolRequest : Message
    {
        public bool data;
    }

    [Serializable]
    public class SetBoolResponse : Message
    {
        public bool   success;
        public string message;
    }
}