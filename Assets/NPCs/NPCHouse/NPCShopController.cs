using UnityEngine;

/// <summary>
/// Master controller for the full-screen NPC shop overlay.
/// Attach to a persistent GameObject (e.g. shop canvas root or a manager object).
/// The shopCanvas should be DISABLED by default in the scene.
///
/// Press M during Exploration → opens the shop and pauses the game.
/// Press M (or click Leave) while the shop is open → closes it and resumes play.
/// </summary>
public class NPCShopController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The root Canvas / GameObject for the full-screen shop UI. Starts disabled.")]
    [SerializeField] private GameObject shopCanvas;

    [Tooltip("The interior menu that manages Talk / Shop / Leave sub-panels.")]
    [SerializeField] private InteriorMenuController menuController;

    [Header("Settings")]
    [Tooltip("Key used to open / close the shop.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;

    void Start()
    {
        // Make sure the canvas is off at the start
        if (shopCanvas != null)
            shopCanvas.SetActive(false);
    }

    void Update()
    {
        if (!Input.GetKeyDown(toggleKey)) return;
        if (GameManager.Instance == null) return;

        var state = GameManager.Instance.currentState;

        if (state == GameManager.GameState.Exploration)
        {
            OpenShop();
        }
        else if (state == GameManager.GameState.ShopMenu)
        {
            CloseShop();
        }
    }

    /// <summary>
    /// Opens the full-screen shop, pauses the game, and shows the cursor.
    /// </summary>
    public void OpenShop()
    {
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
    }
}
