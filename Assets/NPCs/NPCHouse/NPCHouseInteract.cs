using UnityEngine;

public class NPCHouseInteract : MonoBehaviour
{
    public GameObject pressEText;
    public GameObject interiorCanvas;
    public PlayerFreeze playerFreeze;

    private bool playerInRange;

    void Start()
    {
        if (pressEText != null) pressEText.SetActive(false);
        if (interiorCanvas != null) interiorCanvas.SetActive(false);
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.M))
        {
            if (interiorCanvas != null)
                interiorCanvas.SetActive(true);

            if (pressEText != null)
                pressEText.SetActive(false);

            if (playerFreeze != null)
                playerFreeze.isFrozen = true;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = true;

        if (pressEText != null)
            pressEText.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;

        if (pressEText != null)
            pressEText.SetActive(false);
    }
}