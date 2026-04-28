using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SaveLoadMenuController : MonoBehaviour
{
    public enum MenuMode { None, LoadMode, CreateMode }

    private MenuMode currentMode = MenuMode.None;

    [Header("UI References")]
    public SaveSlotUI[] saveSlots;
    public Button loadModeButton;
    public Button createModeButton;
    public Button returnButton;

    private MainMenuManager mainMenuManager;
    private GameObject returnSelection;
    private bool initialized;
    private bool ownsFocus;

    private void Start()
    {
        Initialize();
    }

    public void Open(MainMenuManager owner, GameObject selectionToRestore)
    {
        mainMenuManager = owner;
        returnSelection = selectionToRestore;

        Initialize();
        SetMode(MenuMode.None);
        RefreshSlots();
        gameObject.SetActive(true);
        ownsFocus = true;
        ConfigureNavigation();
        SelectDefaultButton();
    }

    private void Update()
    {
        if (!ownsFocus || EventSystem.current == null)
        {
            return;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !IsSelectionInsideMenu(selected))
        {
            SelectDefaultButton();
        }
    }

    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        CacheReferences();

        for (int i = 0; i < saveSlots.Length; i++)
        {
            if (saveSlots[i] != null)
            {
                saveSlots[i].Initialize(i, this);
            }
        }

        if (loadModeButton != null)
        {
            loadModeButton.onClick.RemoveListener(SetLoadMode);
            loadModeButton.onClick.AddListener(SetLoadMode);
        }

        if (createModeButton != null)
        {
            createModeButton.onClick.RemoveListener(SetCreateMode);
            createModeButton.onClick.AddListener(SetCreateMode);
        }

        if (returnButton != null)
        {
            returnButton.onClick.RemoveListener(Close);
            returnButton.onClick.AddListener(Close);
        }

        ConfigureNavigation();
        initialized = true;
    }

    public void SetMode(MenuMode newMode)
    {
        currentMode = newMode;
        RefreshSlots();
        Debug.Log("Save/Load Mode set to: " + currentMode);
    }

    public void HandleSlotClicked(SaveSlotUI clickedSlot)
    {
        if (clickedSlot == null)
        {
            return;
        }

        if (currentMode == MenuMode.None)
        {
            Debug.Log("Select Create or Load first!");
            return;
        }

        if (currentMode == MenuMode.LoadMode)
        {
            if (clickedSlot.hasData)
            {
                Debug.Log("Loading game from slot " + clickedSlot.slotIndex);
                mainMenuManager?.LoadGameFromSlot(clickedSlot.slotIndex);
            }
            else
            {
                Debug.Log("Cannot load an empty slot.");
            }
        }
        else if (currentMode == MenuMode.CreateMode)
        {
            Debug.Log("Creating new game in slot " + clickedSlot.slotIndex);
            mainMenuManager?.CreateNewGameInSlot(clickedSlot.slotIndex);
        }
    }

    public void Close()
    {
        ownsFocus = false;
        gameObject.SetActive(false);

        if (EventSystem.current != null && returnSelection != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(returnSelection);
        }
    }

    private void ConfigureNavigation()
    {
        Button[] navigationButtons = GetNavigationButtons();
        if (navigationButtons.Length == 0)
        {
            return;
        }

        for (int i = 0; i < navigationButtons.Length; i++)
        {
            if (navigationButtons[i] == null)
            {
                continue;
            }

            Navigation navigation = navigationButtons[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = navigationButtons[(i - 1 + navigationButtons.Length) % navigationButtons.Length];
            navigation.selectOnDown = navigationButtons[(i + 1) % navigationButtons.Length];
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            navigationButtons[i].navigation = navigation;
        }

        if (loadModeButton != null && createModeButton != null)
        {
            Navigation loadNavigation = loadModeButton.navigation;
            loadNavigation.selectOnRight = createModeButton;
            loadModeButton.navigation = loadNavigation;

            Navigation createNavigation = createModeButton.navigation;
            createNavigation.selectOnLeft = loadModeButton;
            createModeButton.navigation = createNavigation;
        }
    }

    private void SetLoadMode()
    {
        SetMode(MenuMode.LoadMode);
    }

    private void SetCreateMode()
    {
        SetMode(MenuMode.CreateMode);
    }

    private void RefreshSlots()
    {
        if (saveSlots == null)
        {
            return;
        }

        foreach (SaveSlotUI saveSlot in saveSlots)
        {
            if (saveSlot != null)
            {
                saveSlot.RefreshSlotData();
            }
        }
    }

    private void CacheReferences()
    {
        if (loadModeButton == null)
        {
            loadModeButton = FindButtonByName("LoadButton");
        }

        if (createModeButton == null)
        {
            createModeButton = FindButtonByName("CreateButton");
        }

        if (returnButton == null)
        {
            returnButton = FindButtonByName("CloseButton");
        }

        if (saveSlots == null || saveSlots.Length == 0)
        {
            Transform slotContainer = FindChildByName(transform, "SlotContainer");
            if (slotContainer != null)
            {
                saveSlots = slotContainer.GetComponentsInChildren<SaveSlotUI>(true);
                if (saveSlots.Length == 0)
                {
                    Button[] slotButtons = slotContainer.GetComponentsInChildren<Button>(true);
                    saveSlots = new SaveSlotUI[slotButtons.Length];
                    for (int i = 0; i < slotButtons.Length; i++)
                    {
                        SaveSlotUI slotUI = slotButtons[i].GetComponentInParent<SaveSlotUI>();
                        if (slotUI == null)
                        {
                            Transform slotRoot = slotButtons[i].transform.parent != null
                                ? slotButtons[i].transform.parent
                                : slotButtons[i].transform;
                            slotUI = slotRoot.gameObject.AddComponent<SaveSlotUI>();
                        }

                        slotUI.slotButton = slotButtons[i];
                        saveSlots[i] = slotUI;
                    }
                }
            }
        }
    }

    private void SelectDefaultButton()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        GameObject selection = loadModeButton != null ? loadModeButton.gameObject : null;
        if (selection == null && saveSlots != null && saveSlots.Length > 0 && saveSlots[0] != null)
        {
            selection = saveSlots[0].gameObject;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selection);
    }

    private bool IsSelectionInsideMenu(GameObject selected)
    {
        return selected != null && selected.transform.IsChildOf(transform);
    }

    private Button[] GetNavigationButtons()
    {
        System.Collections.Generic.List<Button> buttons = new System.Collections.Generic.List<Button>();

        if (loadModeButton != null)
        {
            buttons.Add(loadModeButton);
        }

        if (createModeButton != null)
        {
            buttons.Add(createModeButton);
        }

        if (saveSlots != null)
        {
            for (int i = 0; i < saveSlots.Length; i++)
            {
                if (saveSlots[i] != null && saveSlots[i].slotButton != null)
                {
                    buttons.Add(saveSlots[i].slotButton);
                }
            }
        }

        if (returnButton != null)
        {
            buttons.Add(returnButton);
        }

        return buttons.ToArray();
    }

    private Button FindButtonByName(string buttonName)
    {
        Transform buttonTransform = FindChildByName(transform, buttonName);
        return buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
    }

    private Transform FindChildByName(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildByName(parent.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
