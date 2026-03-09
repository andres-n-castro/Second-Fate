using UnityEngine;

public class InteractionMenuUI : MonoBehaviour
{
    [SerializeField] GameObject pressEText;
    [SerializeField] GameObject menuPanel;
    [SerializeField] KeyCode interactKey = KeyCode.E;

    NPC currentNPC;
    CanvasGroup menuGroup;
    bool panelIsSameObject;

    void Awake()
    {
        if (pressEText) pressEText.SetActive(false);

        if (menuPanel != null)
        {
            panelIsSameObject = (menuPanel == gameObject);

            menuGroup = menuPanel.GetComponent<CanvasGroup>();
            if (menuGroup == null) menuGroup = menuPanel.AddComponent<CanvasGroup>();

            if (panelIsSameObject)
            {
                SetMenuVisible(false);
            }
            else
            {
                menuPanel.SetActive(false);
                SetMenuVisible(false);
            }
        }
    }

    void Update()
    {
        if (currentNPC != null && Input.GetKeyDown(interactKey))
        {
            if (!IsMenuOpen() && currentNPC.IsBusy())
            {
                RefreshPrompt();
                return;
            }

            ToggleMenu();
        }

        RefreshPrompt();
    }

    public void SetNearbyNPC(NPC npc)
    {
        currentNPC = npc;
        RefreshPrompt();
    }

    public void ClearNPC()
    {
        currentNPC = null;
        if (pressEText) pressEText.SetActive(false);
        CloseMenu();
    }

    public void ToggleMenu()
    {
        if (currentNPC == null || menuPanel == null) return;

        if (IsMenuOpen()) CloseMenu();
        else OpenMenu();

        RefreshPrompt();
    }

    void OpenMenu()
    {
        if (menuPanel == null) return;

        if (!panelIsSameObject && !menuPanel.activeSelf)
            menuPanel.SetActive(true);

        SetMenuVisible(true);
    }

    void CloseMenu()
    {
        if (menuPanel == null) return;

        SetMenuVisible(false);

        if (!panelIsSameObject && menuPanel.activeSelf)
            menuPanel.SetActive(false);
    }

    public bool IsMenuOpen()
    {
        if (menuPanel == null) return false;

        if (!panelIsSameObject && !menuPanel.activeSelf)
            return false;

        return menuGroup != null && menuGroup.alpha > 0.5f;
    }

    public void Talk()
    {
        if (currentNPC == null) return;
        CloseMenu();
        currentNPC.StartTalk();
        RefreshPrompt();
    }

    public void Shop()
    {
        if (currentNPC == null) return;
        CloseMenu();
        currentNPC.OpenShop();
        RefreshPrompt();
    }

    void RefreshPrompt()
    {
        if (pressEText == null) return;

        bool near = currentNPC != null;
        bool menuOpen = IsMenuOpen();
        bool npcBusy = currentNPC != null && currentNPC.IsBusy();

        pressEText.SetActive(near && !menuOpen && !npcBusy);
    }

    void SetMenuVisible(bool visible)
    {
        if (menuGroup == null) return;
        menuGroup.alpha = visible ? 1f : 0f;
        menuGroup.interactable = visible;
        menuGroup.blocksRaycasts = visible;
    }
}