using UnityEngine;
public class ListMics : MonoBehaviour
{
    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("ListMics No se detectó ningún micrófono.");
            return;
        }

        Debug.Log("ListMics Micrófonos disponibles:");
        foreach (string dev in Microphone.devices)
            Debug.Log($"ListMics   -> {dev}");
    }
}