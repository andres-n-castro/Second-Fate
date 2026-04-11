using UnityEngine;

public class CharmPickup : MonoBehaviour
{
    [Header("The Charm to Unlock")]
    public CharmData charmToGrant;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Make sure we have a charm assigned and the manager exists
            if (charmToGrant != null && CharmManager.Instance != null)
            {
                // Check if the player already has this charm to prevent duplicates
                if (!CharmManager.Instance.unlockedCharms.Contains(charmToGrant))
                {
                    CharmManager.Instance.unlockedCharms.Add(charmToGrant);
                    Debug.Log("Unlocked Charm: " + charmToGrant.charmName);
                }

                // Destroy the physical drop regardless
                Destroy(gameObject);
            }
        }
    }
}
