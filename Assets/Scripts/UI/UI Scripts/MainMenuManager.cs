using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Menu UI")]
    public GameObject arrowSelect;
    public Button startButton;
    public Button settingsButton;
    public Button loadGameButton;
    public Button quitButton;

    [Header("References")]
    public OptionsManager optionsMenuPrefab; // Reference to the OptionsUI in scene
    public GameObject controlsCanvasPrefab;

    [Header("Level Loader")]
    public RectTransform levelLoaderPanel;
    public Image levelLoaderImage;
    public TextMeshProUGUI biomeNameText;
    public GameObject loadingSpinner;
    public Sprite[] levelBackgrounds;
    public float loaderSlideDuration = 0.8f;
    private bool isLevelLoading = false;
    private bool isPaused = false;
    private GameObject controlsCanvasInstance;
    void Start()
    {
        if (levelLoaderPanel != null)
        {
            levelLoaderPanel.anchoredPosition = new Vector2(Screen.width, 0);
            levelLoaderPanel.gameObject.SetActive(false);
        }

        if (loadingSpinner != null) loadingSpinner.SetActive(false);
        DisableStartupBlockers();

        SetupButtonNavigation();
        
        // Setup button clicks
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
            startButton.onClick.AddListener(OnStartClicked);
        }
        
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenControls);
            settingsButton.onClick.AddListener(OpenControls);
        }

        if (loadGameButton != null)
        {
            loadGameButton.onClick.RemoveListener(OnLoadGameClicked);
            loadGameButton.onClick.AddListener(OnLoadGameClicked);
        }
        
        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        SelectDefaultButton();

        // If this menu starts active, pause the game immediately
        if (gameObject.activeSelf)
        {
            PauseGame();
        }
    }

    void Update()
    {
        if (isLevelLoading) return;

        // Arrow Movement
        if (arrowSelect != null)
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (ShouldReclaimMenuSelection(selected))
            {
                SelectDefaultButton();
                selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            }

            if (IsMenuButton(selected))
            {
                RectTransform buttonRect = selected.GetComponent<RectTransform>();
                arrowSelect.transform.position = new Vector3(
                    buttonRect.position.x + 180, 
                    buttonRect.position.y + 30, 
                    buttonRect.position.z);
            }
        }

        // Space to click
        if (Input.GetKeyDown(KeyCode.Escape) && !isLevelLoading)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }

        if (isLevelLoading || !isPaused) return;
    }

public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // This freezes physics and animations
        gameObject.SetActive(true);
        
        // Force the EventSystem to focus on the start button for keyboard/controller support
        SelectDefaultButton();
            
        // Optional: Cursor.lockState = CursorLockMode.None; 
        // Optional: Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // Resumes the game world
        gameObject.SetActive(false);
    }
    public void OnStartClicked()
    {
        Time.timeScale = 1f;
        if (!isLevelLoading) StartCoroutine(TransitionToLevel());
    }

    public void OnLoadGameClicked()
    {
        if (isLevelLoading)
        {
            return;
        }

        Debug.Log("Load Game UI is not hooked up yet.");
    }

    public void OnQuitClicked()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    public void ToggleOptions()
    {
        OpenControls();
    }

    public void OpenControls()
    {
        if (controlsCanvasInstance == null)
        {
            GameObject existingControls = GameObject.Find("ControlsCanvas");
            if (existingControls != null)
            {
                controlsCanvasInstance = existingControls;
            }
            else if (controlsCanvasPrefab != null)
            {
                controlsCanvasInstance = Instantiate(controlsCanvasPrefab);
            }
        }

        if (controlsCanvasInstance != null)
        {
            ControlsCanvasController controlsCanvasController = controlsCanvasInstance.GetComponent<ControlsCanvasController>();
            if (controlsCanvasController != null)
            {
                GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                Button fallbackButton = GetFirstAvailableButton();
                controlsCanvasController.Open(selected != null ? selected : fallbackButton != null ? fallbackButton.gameObject : null);
                return;
            }

            controlsCanvasInstance.SetActive(true);
        }
    }

    IEnumerator TransitionToLevel()
{
    isLevelLoading = true;

    // 1. Show the Level Loader
    if (levelLoaderPanel != null)
    {
        levelLoaderPanel.gameObject.SetActive(true);
        if (biomeNameText != null) biomeNameText.text = "Traveling to: Midgard...";
        yield return StartCoroutine(SlidePanel(levelLoaderPanel, 0, loaderSlideDuration));
    }

    // 2. IMPORTANT: Hide the Main Menu elements behind the loader
    // This stops them from "coexisting" with the game once it loads
    if (startButton != null) startButton.transform.parent.gameObject.SetActive(false); 
    // Or use a direct reference to your 'MenuContent' object

    yield return new WaitForSeconds(0.2f);
    if (loadingSpinner != null) loadingSpinner.SetActive(true);
    yield return new WaitForSeconds(2.0f);

    // 3. Load the level
    // Since this script is likely on a prefab, ensure you don't have 'DontDestroyOnLoad' 
    // on the UI if you want it to disappear completely.
    ContinueOrStartDefaultGame();
}

    IEnumerator SlidePanel(RectTransform panel, float targetX, float duration)
    {
        float t = 0f;
        Vector2 start = panel.anchoredPosition;
        Vector2 end = new Vector2(targetX, 0);
        while (t < duration)
        {
            t += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, t / duration);
            panel.anchoredPosition = Vector2.Lerp(start, end, smooth);
            yield return null;
        }
        panel.anchoredPosition = end;
    }

    void SetupButtonNavigation()
    {
        Button[] buttons = GetMenuButtons();
        if (buttons.Length == 0)
        {
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Navigation navigation = buttons[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = buttons[(i - 1 + buttons.Length) % buttons.Length];
            navigation.selectOnDown = buttons[(i + 1) % buttons.Length];
            buttons[i].navigation = navigation;
        }
    }

    private Button[] GetMenuButtons()
    {
        System.Collections.Generic.List<Button> buttons = new System.Collections.Generic.List<Button>();

        if (startButton != null)
        {
            buttons.Add(startButton);
        }

        if (loadGameButton != null)
        {
            buttons.Add(loadGameButton);
        }

        if (settingsButton != null)
        {
            buttons.Add(settingsButton);
        }

        if (quitButton != null)
        {
            buttons.Add(quitButton);
        }

        return buttons.ToArray();
    }

    private bool IsMenuButton(GameObject selected)
    {
        if (selected == null)
        {
            return false;
        }

        Button[] buttons = GetMenuButtons();
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].gameObject == selected)
            {
                return true;
            }
        }

        return false;
    }

    private void SelectDefaultButton()
    {
        Button defaultButton = startButton != null ? startButton : GetFirstAvailableButton();
        if (EventSystem.current == null || defaultButton == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
    }

    private Button GetFirstAvailableButton()
    {
        Button[] buttons = GetMenuButtons();
        return buttons.Length > 0 ? buttons[0] : null;
    }

    private bool ShouldReclaimMenuSelection(GameObject selected)
    {
        if (!gameObject.activeInHierarchy || isLevelLoading)
        {
            return false;
        }

        if (controlsCanvasInstance != null && controlsCanvasInstance.activeInHierarchy)
        {
            return false;
        }

        return selected == null || !IsMenuButton(selected);
    }

    private void DisableStartupBlockers()
    {
        if (levelLoaderPanel != null)
        {
            levelLoaderPanel.gameObject.SetActive(false);
        }

        DisableChildBlocker("OptionsDimmer");
        DisableChildBlocker("ControlsCanvas");
        DisableChildBlocker("LoadGameSectionCanvas");
    }

    private void DisableChildBlocker(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name != childName)
            {
                continue;
            }

            CanvasGroup canvasGroup = children[i].GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            Graphic graphic = children[i].GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = false;
            }

            children[i].gameObject.SetActive(false);
        }
    }

    private void ContinueOrStartDefaultGame()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.ContinueOrStartDefaultGame();
        }
        else
        {
            SceneManager.LoadScene("tutorial_hub");
        }
    }
}
