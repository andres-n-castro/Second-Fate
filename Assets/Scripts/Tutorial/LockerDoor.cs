using UnityEngine;

public class LockerDoor : MonoBehaviour
{
    public string doorID;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (TutorialManager.Instance.HasKey(doorID))
            {
                Debug.Log($"Door {doorID} unlocked!");
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Door is locked. You need a specific key.");
            }
        }
    }
}
