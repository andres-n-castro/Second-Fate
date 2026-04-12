using UnityEngine;

public class NPCHouse : MonoBehaviour
{
    public GameObject interiorCanvas;
    public GameObject pressEText;
    public PlayerFreeze playerFreeze;

    bool playerInRange;

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.M))
        {
            EnterHouse();
        }
    }

    void EnterHouse()
    {
        interiorCanvas.SetActive(true);
        playerFreeze.isFrozen = true;
        pressEText.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            pressEText.SetActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            pressEText.SetActive(false);
        }
    }
}