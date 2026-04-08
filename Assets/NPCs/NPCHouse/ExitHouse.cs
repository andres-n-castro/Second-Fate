using UnityEngine;

public class ExitHouse : MonoBehaviour
{
    public GameObject interiorCanvas;
    public PlayerFreeze playerFreeze;

    public void Exit()
    {
        if (interiorCanvas != null)
            interiorCanvas.SetActive(false);

        if (playerFreeze != null)
            playerFreeze.isFrozen = false;
    }
}