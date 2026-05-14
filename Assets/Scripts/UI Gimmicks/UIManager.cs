using UnityEngine;
using TMPro;
using UnityEngine.UI;
using RosSharp.RosBridgeClient;

public class UIManager : MonoBehaviour
{
    [Header("Image Receiver")]
    public RGBDImageReceiverUDP imageReceiver;
    private bool showingRGB = true;

    [Header("Map Manager")]
    public Map3DManager mapManager;

    [Header("UI Elements")]
    public TMP_Dropdown mapDropdown;

    [Header("Audio Controls")]
    public Button muteButton;
    public Button deafenButton;

    public Sprite micOnSprite;
    public Sprite micOffSprite;
    public Sprite headphonesOnSprite;
    public Sprite headphonesOffSprite;

    private bool isMuted = false;
    private bool isDeafened = false;

    public AudioSenderMobile audioSender;
    public AudioReceiverMobile audioReceiver;

    private float lastAudioToggleTime = 0f;
    private const float audioToggleCooldown = 0.3f;

    [Header("Minigame manager")]
    public GameModeManager gameManager;

    private RosSocket rosSocket;

    void Start()
    {
        showingRGB = true;

        if (imageReceiver != null)
            imageReceiver.ShowRGB();

        // MAP DEFAULT
        if (mapManager != null)
            mapManager.ShowWallMap();

        SetupDropdown();

        if (muteButton != null)
            muteButton.onClick.AddListener(ToggleMute);

        if (deafenButton != null)
            deafenButton.onClick.AddListener(ToggleDeafen);

        UpdateMicIcon();
        UpdateHeadphonesIcon();

        // Obtén el RosSocket
        var connector = FindObjectOfType<RosConnector>();
        if (connector != null)
            rosSocket = connector.RosSocket;
    }

    // =========================
    // DROPDOWN SETUP
    // =========================
    void SetupDropdown()
    {
        if (mapDropdown == null) return;

        mapDropdown.ClearOptions();
        mapDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "Wall Map",
            "Mesh Map",
            "Octo Map",
            "Particle Map"
        });

        mapDropdown.onValueChanged.AddListener(SetActiveMap);
    }

    // =========================
    // RGB / DEPTH
    // =========================
    public void ToggleScreen()
    {
        showingRGB = !showingRGB;

        if (imageReceiver != null)
        {
            if (showingRGB)
                imageReceiver.ShowRGB();
            else
                imageReceiver.ShowDepth();
        }

        // PUBLICA A ROS
        if (rosSocket != null)
        {
            var msg = new RosSharp.RosBridgeClient.MessageTypes.Std.String { data = showingRGB ? "rgb" : "depth" };
            rosSocket.Publish("/camera_stream_mode", msg);
        }

        Debug.Log("UIManager: Modo -> " + (showingRGB ? "RGB" : "Depth"));
    }

    // =========================
    // MAP SELECTION
    // =========================
    public void SetActiveMap(int index)
    {
        if (mapManager == null)
            return;

        switch (index)
        {
            case 0:
                mapManager.ShowWallMap();
                break;

            case 1:
                mapManager.ShowMeshMap();
                break;

            case 2:
                mapManager.ShowOctoMap();
                break;

            case 3:
                mapManager.ShowParticleMap();
                break;

            default:
                mapManager.ShowWallMap();
                break;
        }

        Debug.Log($"UIManager: Mapa activo -> {GetMapName(index)}");
    }

    private string GetMapName(int index)
    {
        return index switch
        {
            0 => "Wall Map",
            1 => "Mesh Map",
            2 => "Octo Map",
            3 => "Particle Map",
            _ => "Unknown"
        };
    }

    // =========================
    // AUDIO CONTROLS
    // =========================
    public void ToggleMute()
    {
        if (Time.time - lastAudioToggleTime < audioToggleCooldown)
            return;

        lastAudioToggleTime = Time.time;
        isMuted = !isMuted;

        if (audioSender != null)
            audioSender.isMuted = isMuted;

        UpdateMicIcon();
    }

    public void ToggleDeafen()
    {
        if (Time.time - lastAudioToggleTime < audioToggleCooldown)
            return;

        lastAudioToggleTime = Time.time;
        isDeafened = !isDeafened;

        if (audioReceiver != null)
            audioReceiver.isDeafened = isDeafened;

        UpdateHeadphonesIcon();
    }

    private void UpdateMicIcon()
    {
        if (muteButton != null)
        {
            Image img = muteButton.GetComponent<Image>();
            if (img != null)
                img.sprite = isMuted ? micOffSprite : micOnSprite;
        }
    }

    private void UpdateHeadphonesIcon()
    {
        if (deafenButton != null)
        {
            Image img = deafenButton.GetComponent<Image>();
            if (img != null)
                img.sprite = isDeafened ? headphonesOffSprite : headphonesOnSprite;
        }
    }

    // =========================
    // MINIGAME
    // =========================
    public void OnGameModeButton()
    {
        gameManager.StartGameMode();
    }
}