using UnityEngine;

public class GameManager : MonoBehaviour
{
    //public GameObject cabinMode;
    //public GameObject immersiveMode;

    private bool isCabinMode = true;

    public void ToggleMode()
    {
        isCabinMode = !isCabinMode;
        //cabinMode.SetActive(isCabinMode);
        //immersiveMode.SetActive(!isCabinMode);

        Debug.Log("Modo cambiado a: " + (isCabinMode ? "Cabina" : "Inmersivo"));
    }
}
