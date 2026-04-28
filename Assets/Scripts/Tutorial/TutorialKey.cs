using UnityEngine;

public class TutorialKey : MonoBehaviour
{
    [Tooltip("The ID must match the Door ID it unlocks.")]
    public string keyID;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            TutorialManager.EnsureInstance().AddKey(keyID);
            Debug.Log($"Picked up key: {keyID}");
            SaveManager.Instance?.SaveCurrentSlot();
            Destroy(gameObject);
        }
    }
}
