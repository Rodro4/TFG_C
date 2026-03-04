using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Display Screens")]
    public GameObject rgbScreen;
    public GameObject depthScreen;

    private bool showingRGB = true;

    [Header("3D Maps")]
    public GameObject[] pointCloudObjects;
    public GameObject octoMap;
    public GameObject occupancyMap;

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

    private AudioUDPSender audioSender;
    private AudioUDPReceiver audioReceiver;

    private float lastAudioToggleTime = 0f;
    private const float audioToggleCooldown = 0.3f;

    [Header("Minigame manager")]
    public GameModeManager gameManager;

    void Start()
    {
        audioSender = FindObjectOfType<AudioUDPSender>();
        audioReceiver = FindObjectOfType<AudioUDPReceiver>();

        showingRGB = true;
        SetScreenState(showingRGB);

        SetActiveMap(2);

        if (mapDropdown != null)
            mapDropdown.onValueChanged.AddListener(SetActiveMap);

        if (muteButton != null)
            muteButton.onClick.AddListener(ToggleMute);

        if (deafenButton != null)
            deafenButton.onClick.AddListener(ToggleDeafen);

        UpdateMicIcon();
        UpdateHeadphonesIcon();
    }

    // RGB / DEPTH
    public void ToggleScreen()
    {
        showingRGB = !showingRGB;
        SetScreenState(showingRGB);
    }

    private void SetScreenState(bool rgb)
    {
        if (rgbScreen != null)
            rgbScreen.SetActive(rgb);

        if (depthScreen != null)
            depthScreen.SetActive(!rgb);

        Debug.Log("UIManager: Pantalla activa -> " + (rgb ? "RGB" : "Depth"));
    }


    // 3D MAP SELECTION
    public void SetActiveMap(int index)
    {
        SetGroupActive(pointCloudObjects, index == 0);

        if (octoMap != null)
            octoMap.SetActive(index == 1);

        if (occupancyMap != null)
            occupancyMap.SetActive(index == 2);

        Debug.Log($"UIManager: Mapa 3D activo -> {GetMapName(index)}");
    }

    private void SetGroupActive(GameObject[] group, bool active)
    {
        if (group == null) return;

        foreach (var go in group)
            if (go != null)
                go.SetActive(active);
    }

    private string GetMapName(int index)
    {
        return index switch
        {
            0 => "PointCloud (+ PersistentVoxelMap)",
            1 => "OctoMap",
            2 => "OccupancyMap",
            _ => "Unknown"
        };
    }

    // AUDIO CONTROLS
    public void ToggleMute()
    {
        if (Time.time - lastAudioToggleTime < audioToggleCooldown)
            return;

        lastAudioToggleTime = Time.time;
        isMuted = !isMuted;

        if (audioSender != null)
            audioSender.enabled = !isMuted;

        UpdateMicIcon();
    }

    public void ToggleDeafen()
    {
        if (Time.time - lastAudioToggleTime < audioToggleCooldown)
            return;

        lastAudioToggleTime = Time.time;
        isDeafened = !isDeafened;

        if (audioReceiver != null)
            audioReceiver.enabled = !isDeafened;

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

    // MINIGAME START
    public void OnGameModeButton()
    {
        gameManager.StartGameMode();
    }

}
