using UnityEngine;

public class LockerDoor : MonoBehaviour
{
    public string doorID;
    [SerializeField] public string uniqueInteractionID;

    [Tooltip("If true, the door is only disabled (SetActive(false)) when unlocked, so it can be re-activated later (e.g. by BossArenaController). If false, the door is destroyed permanently.")]
    public bool disableInsteadOfDestroy;

    private bool hasBeenUnlocked;
    private string RuntimePersistentID => string.IsNullOrEmpty(uniqueInteractionID) && SaveManager.Instance != null
        ? SaveManager.Instance.BuildSceneObjectID(gameObject)
        : uniqueInteractionID;

    private void Start()
    {
        if (SaveManager.Instance != null && SaveManager.Instance.IsInteractableLooted(RuntimePersistentID))
        {
            if (disableInsteadOfDestroy)
            {
                gameObject.SetActive(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasBeenUnlocked) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        if (TutorialManager.Instance.HasKey(doorID))
        {
            Debug.Log($"Door {doorID} unlocked!");
            hasBeenUnlocked = true;
            SaveManager.Instance?.MarkInteractableLooted(RuntimePersistentID);

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
