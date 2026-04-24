using UnityEngine;

public class LockerDoor : MonoBehaviour
{
    public string doorID;

    [Tooltip("If true, the door is only disabled (SetActive(false)) when unlocked, so it can be re-activated later (e.g. by BossArenaController). If false, the door is destroyed permanently.")]
    public bool disableInsteadOfDestroy;

    private bool hasBeenUnlocked;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasBeenUnlocked) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        if (TutorialManager.Instance.HasKey(doorID))
        {
            Debug.Log($"Door {doorID} unlocked!");
            hasBeenUnlocked = true;

            if (disableInsteadOfDestroy)
                gameObject.SetActive(false);
            else
                Destroy(gameObject);
        }
        else
        {
            Debug.Log("Door is locked. You need a specific key.");
        }
    }
}
