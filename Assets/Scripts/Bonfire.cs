using UnityEngine;

public class Bonfire : MonoBehaviour
{
    public string bonfireID;

    public Item treeEssenceItem;
    public Item creatureBloodItem;

    private bool isPlayerInRange = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            // TODO: Show "Press E to Interact" UI prompt.
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            // TODO: Hide interaction UI prompt.
        }
    }

    void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        {
            InteractWithBonfire();
        }
    }

    private void InteractWithBonfire()
    {
        GameManager.AlignmentType currentAlignment = GameManager.Instance.GetBonfireAlignment(bonfireID);

        if (currentAlignment != GameManager.AlignmentType.None)
        {
            GameManager.Instance.UnlockBonfire(bonfireID);
            GameManager.Instance.currentRespawnPoint = transform.position;
            PlayerManager.Instance.playerStats.currentHealth = PlayerManager.Instance.playerStats.maxHealth;
            PlayerManager.Instance.playerStats.SyncHealthForSaving(
                PlayerManager.Instance.playerStats.maxHealth,
                PlayerManager.Instance.playerStats.maxHealth);

            if (PlayerManager.Instance.playerStats.playerHealthComponent != null)
            {
                PlayerManager.Instance.playerStats.playerHealthComponent.InitializeHealth(
                    PlayerManager.Instance.playerStats.maxHealth,
                    PlayerManager.Instance.playerStats.maxHealth);
            }

            PlayerManager.Instance.ResetProtectionCharmCharges();

            // TODO: Call World Reset logic (Respawn all non-boss enemies).

            Debug.Log("Rested at Bonfire. Health restored and enemies reset.");
        }
        else
        {
            GameManager.Instance.UnlockBonfire(bonfireID);
            var inventory = InventoryController.Instance.inventoryModel.RetrieveInventoryItems();

            bool hasEssence = false;
            bool hasBlood = false;

            foreach (ItemSlotData slot in inventory)
            {
                if (slot.itemData == treeEssenceItem)
                {
                    hasEssence = true;
                }

                if (slot.itemData == creatureBloodItem)
                {
                    hasBlood = true;
                }
            }

            if (!hasEssence && !hasBlood)
            {
                // TODO: Tell UIManager to flash warning "Requires Essence or Blood to ignite."
                Debug.Log("Cannot ignite. Missing items.");
            }
            else
            {
                GameManager.Instance.lastInteractedBonfireID = bonfireID;
                GameManager.Instance.ChangeState(GameManager.GameState.BonfireMenu);
            }
        }
    }
}
