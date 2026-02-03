//using UnityEngine;
//using TMPro;
//using UnityEngine.UI;

//public class UIManager : MonoBehaviour
//{
//    // CAMERA / SCREEN UI
//    private VREyeFeed eyeFeed;

//    [Header("Display Screens")]
//    public GameObject rgbScreen;
//    public GameObject depthScreen;

//    private bool showingRGB = true;

//    // 3D MAP UI
//    [Header("3D Maps")]
//    [Tooltip("PointCloud + PersistentVoxelMap")]
//    public GameObject[] pointCloudObjects;

//    public GameObject octoMap;
//    public GameObject occupancyMap;

//    [Header("UI Elements")]
//    public TMP_Dropdown mapDropdown;

//    // AUDIO UI
//    [Header("Audio Controls")]
//    public Button muteButton;
//    public Button deafenButton;

//    public Sprite micOnSprite;
//    public Sprite micOffSprite;

//    public Sprite headphonesOnSprite;
//    public Sprite headphonesOffSprite;

//    private bool isMuted = false;
//    private bool isDeafened = false;

//    private AudioUDPSender audioSender;
//    private AudioUDPReceiver audioReceiver;

//    // Controlar los dobles clicks
//    private float lastAudioToggleTime = 0f;
//    private const float audioToggleCooldown = 0.3f;


//    void Start()
//    {
//        eyeFeed = FindObjectOfType<VREyeFeed>();
//        if (eyeFeed == null)
//            Debug.LogWarning("UIManager: VREyeFeed component not found.");

//        audioSender = FindObjectOfType<AudioUDPSender>();
//        audioReceiver = FindObjectOfType<AudioUDPReceiver>();

//        if (audioSender == null)
//            Debug.LogWarning("UIManager: AudioUDPSender not found.");
//        if (audioReceiver == null)
//            Debug.LogWarning("UIManager: AudioUDPReceiver not found.");

//        SetScreenState(true);
//        SetActiveMap(0);

//        if (mapDropdown != null)
//            mapDropdown.onValueChanged.AddListener(SetActiveMap);

//        if (muteButton != null)
//            muteButton.onClick.AddListener(ToggleMute);

//        if (deafenButton != null)
//            deafenButton.onClick.AddListener(ToggleDeafen);

//        UpdateMicIcon();
//        UpdateHeadphonesIcon();
//    }

//    // RGB / DEPTH
//    public void ToggleScreen()
//    {
//        showingRGB = !showingRGB;
//        SetScreenState(showingRGB);

//        if (eyeFeed != null)
//            eyeFeed.SetScreenSource(showingRGB);
//    }

//    private void SetScreenState(bool rgb)
//    {
//        if (rgbScreen != null) rgbScreen.SetActive(rgb);
//        if (depthScreen != null) depthScreen.SetActive(!rgb);

//        Debug.Log("UIManager: Current screen -> " + (rgb ? "RGB" : "Depth"));
//    }

//    // 3D MAP SELECTION
//    public void SetActiveMap(int index)
//    {
//        SetGroupActive(pointCloudObjects, index == 0);

//        if (octoMap != null)
//            octoMap.SetActive(index == 1);

//        if (occupancyMap != null)
//            occupancyMap.SetActive(index == 2);

//        Debug.Log($"UIManager: Mapa 3D activo -> {GetMapName(index)}");
//    }

//    private void SetGroupActive(GameObject[] group, bool active)
//    {
//        if (group == null) return;

//        foreach (var go in group)
//        {
//            if (go != null)
//                go.SetActive(active);
//        }
//    }

//    private string GetMapName(int index)
//    {
//        return index switch
//        {
//            0 => "PointCloud (+ PersistentVoxelMap)",
//            1 => "OctoMap",
//            2 => "OccupancyMap",
//            _ => "Unknown"
//        };
//    }

//    // AUDIO CONTROLS
//    public void ToggleMute()
//    {
//        if (Time.time - lastAudioToggleTime < audioToggleCooldown)
//            return;

//        lastAudioToggleTime = Time.time;

//        isMuted = !isMuted;

//        if (audioSender != null)
//            audioSender.enabled = !isMuted;

//        UpdateMicIcon();

//        Debug.Log("Muted: " + isMuted);
//    }

//    public void ToggleDeafen()
//    {
//        if (Time.time - lastAudioToggleTime < audioToggleCooldown)
//            return;

//        lastAudioToggleTime = Time.time;

//        isDeafened = !isDeafened;

//        if (audioReceiver != null)
//            audioReceiver.enabled = !isDeafened;

//        UpdateHeadphonesIcon();

//        Debug.Log("Deafened: " + isDeafened);
//    }

//    private void UpdateMicIcon()
//    {
//        if (muteButton != null)
//        {
//            Image img = muteButton.GetComponent<Image>();
//            if (img != null)
//                img.sprite = isMuted ? micOffSprite : micOnSprite;
//        }
//    }

//    private void UpdateHeadphonesIcon()
//    {
//        if (deafenButton != null)
//        {
//            Image img = deafenButton.GetComponent<Image>();
//            if (img != null)
//                img.sprite = isDeafened ? headphonesOffSprite : headphonesOnSprite;
//        }
//    }
//}






















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

    void Start()
    {
        audioSender = FindObjectOfType<AudioUDPSender>();
        audioReceiver = FindObjectOfType<AudioUDPReceiver>();

        showingRGB = true;
        SetScreenState(showingRGB);

        SetActiveMap(0);

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
}
