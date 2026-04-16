using UnityEngine;

/// <summary>
/// Master controller for the full-screen NPC shop overlay.
/// Attach to a persistent GameObject (e.g. shop canvas root or a manager object).
/// The shopCanvas should be DISABLED by default in the scene.
///
/// Press M during Exploration → opens the shop and pauses the game.
/// Press M (or click Leave) while the shop is open → closes it and resumes play.
///
/// A proximity popup (e.g. "Press M to enter") is shown when the player
/// enters the trigger collider on this GameObject, and hidden when they leave.
/// </summary>
public class NPCShopController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The root Canvas / GameObject for the full-screen shop UI. Starts disabled.")]
    [SerializeField] private GameObject shopCanvas;

    [Tooltip("The interior menu that manages Talk / Shop / Leave sub-panels.")]
    [SerializeField] private InteriorMenuController menuController;

    [Header("Proximity Popup")]
    [Tooltip("A UI text object (e.g. 'Press M to enter') shown when the player is near the house. Drag a Text / TextMeshPro GameObject here.")]
    [SerializeField] private GameObject proximityPopup;

    [Header("Settings")]
    [Tooltip("Key used to open / close the shop.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;

    /// <summary>Tracks whether the player is currently inside the trigger zone.</summary>
    private bool playerInRange;

    void Start()
    {
        // Make sure the canvas is off at the start
        if (shopCanvas != null)
            shopCanvas.SetActive(false);

        // Hide the popup at the start
        if (proximityPopup != null)
            proximityPopup.SetActive(false);
    }

    void Update()
    {
        if (!Input.GetKeyDown(toggleKey)) return;
        if (GameManager.Instance == null) return;

        var state = GameManager.Instance.currentState;

        if (state == GameManager.GameState.Exploration && playerInRange)
        {
            OpenShop();
        }
        else if (state == GameManager.GameState.ShopMenu)
        {
            CloseShop();
        }
    }

    // ────────────────────── Trigger Detection ──────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = true;

        if (proximityPopup != null)
            proximityPopup.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;

        if (proximityPopup != null)
            proximityPopup.SetActive(false);
    }

    // ────────────────────── Shop Open / Close ──────────────────────

    /// <summary>
    /// Opens the full-screen shop, pauses the game, and shows the cursor.
    /// </summary>
    public void OpenShop()
    {
        // Hide the popup while the shop is open
        if (proximityPopup != null)
            proximityPopup.SetActive(false);

        if (shopCanvas != null)
            shopCanvas.SetActive(true);

        // Reset the interior menu to its default view (Talk / Shop / Leave)
        if (menuController != null)
            menuController.ShowMenu();

        // Pause the game via the GameManager state system
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(GameManager.GameState.ShopMenu);
    }

    /// <summary>
    /// Closes the shop, resumes the game, and hides the cursor.
    /// Called by pressing M again or by the "Leave" button in the menu.
    /// </summary>
    public void CloseShop()
    {
        if (shopCanvas != null)
            shopCanvas.SetActive(false);

        // Tell the menu controller to clean up (stop dialogue coroutines, etc.)
        if (menuController != null)
            menuController.ForceClose();

        // Restore the previous game state (Exploration)
        if (GameManager.Instance != null)
            GameManager.Instance.RestorePreviousState();

        // Re-show the popup if the player is still in range
        if (playerInRange && proximityPopup != null)
            proximityPopup.SetActive(true);
    }
}
