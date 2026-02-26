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

    [Header("Level Loader")]
    public RectTransform levelLoaderPanel;
    public Image levelLoaderImage;
    public TextMeshProUGUI biomeNameText;
    public GameObject loadingSpinner;
    public Sprite[] levelBackgrounds;
    public float loaderSlideDuration = 0.8f;
    private bool isLevelLoading = false;

    void Start()
    {
        if (levelLoaderPanel != null)
        {
            levelLoaderPanel.anchoredPosition = new Vector2(Screen.width, 0);
            levelLoaderPanel.gameObject.SetActive(false);
        }

        if (loadingSpinner != null) loadingSpinner.SetActive(false);

        SetupButtonNavigation();
        
        // Setup button clicks
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
        
        if (settingsButton != null && optionsMenuPrefab != null)
            settingsButton.onClick.AddListener(() => optionsMenuPrefab.OpenOptions());
        
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    void Update()
    {
        if (isLevelLoading) return;

        // Arrow Movement
        if (arrowSelect != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && (selected == startButton.gameObject || 
                selected == settingsButton.gameObject || 
                selected == loadGameButton.gameObject ||
                selected == quitButton.gameObject))
            {
                RectTransform buttonRect = selected.GetComponent<RectTransform>();
                arrowSelect.transform.position = new Vector3(
                    buttonRect.position.x + 180, 
                    buttonRect.position.y + 30, 
                    buttonRect.position.z);
            }
        }

        // Space to click
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null)
            {
                Button button = selected.GetComponent<Button>();
                if (button != null) button.onClick.Invoke();
            }
        }
    }

    public void OnStartClicked()
    {
        if (!isLevelLoading) StartCoroutine(TransitionToLevel());
    }

    public void OnQuitClicked()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    IEnumerator TransitionToLevel()
    {
        isLevelLoading = true;

        if (levelLoaderImage != null && levelBackgrounds.Length > 0)
        {
            levelLoaderImage.sprite = levelBackgrounds[0];
            levelLoaderImage.color = Color.white;
        }

        if (levelLoaderPanel != null)
        {
            levelLoaderPanel.gameObject.SetActive(true);
            if (biomeNameText != null) biomeNameText.text = "Traveling to: Midgard...";
            yield return StartCoroutine(SlidePanel(levelLoaderPanel, 0, loaderSlideDuration));
        }

        yield return new WaitForSeconds(0.2f);
        if (loadingSpinner != null) loadingSpinner.SetActive(true);
        yield return new WaitForSeconds(2.0f);

        // Load first level
        SceneManager.LoadScene("Level1"); // Change to your first level scene name
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
        // Setup your button navigation here
    }
}
