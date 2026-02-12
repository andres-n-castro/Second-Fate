using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Menu Content Group")]
    public CanvasGroup menuContentGroup;
    public float uiFadeDuration = 0.5f;

    [Header("Background Control")]
    public Image mainBackgroundImage;    
    public Sprite[] levelBackgrounds; 

    [Header("Main Menu UI")]
    public GameObject arrowSelect;
    public Button startButton;
    public Button settingsButton;
    public Button loadGameButton;

    [Header("Options Panel")]
    public RectTransform optionsPanel;
    public Button closeOptionsButton;
    public CanvasGroup optionsDimmer; 
    public float optionsSlideDuration = 0.4f;
    private bool isOptionsOpen = false;

    [Header("Level Loader (The Integrated Slider)")]
    public RectTransform levelLoaderPanel; 
    public Image levelLoaderImage;         
    public TextMeshProUGUI biomeNameText;  
    public GameObject loadingSpinner;      
    public float loaderSlideDuration = 0.8f;
    private bool isLevelLoading = false;   

    void Start()
    {
        // options and level loader panel (level load screen)
        if (optionsPanel != null)
            optionsPanel.anchoredPosition = new Vector2(-optionsPanel.rect.width, 0);

        if (levelLoaderPanel != null)
        {
            levelLoaderPanel.anchoredPosition = new Vector2(Screen.width, 0);
            levelLoaderPanel.gameObject.SetActive(false); 
        }

        if (optionsDimmer != null)
        {
            optionsDimmer.alpha = 0;
            optionsDimmer.gameObject.SetActive(false);
        }

        if (menuContentGroup != null) menuContentGroup.alpha = 1f;
        if (loadingSpinner != null) loadingSpinner.SetActive(false);

        SetupButtonNavigation();
        if (closeOptionsButton != null) closeOptionsButton.onClick.AddListener(ToggleOptions);
        
        EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    void Update()
    {
        if (isLevelLoading) return; 

        if (Input.GetKeyDown(KeyCode.Escape)) ToggleOptions();

        //Arrow Movement
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && (selected == startButton.gameObject || selected == settingsButton.gameObject || selected == loadGameButton.gameObject))
        {
            RectTransform buttonRect = selected.GetComponent<RectTransform>();
            arrowSelect.transform.position = new Vector3(buttonRect.position.x + 180, buttonRect.position.y + 30, buttonRect.position.z);
        }
        
        if (Input.GetKeyDown(KeyCode.Space) && selected != null)
        {
             Button button = selected.GetComponent<Button>();
             if (button != null) button.onClick.Invoke();
        }
    }

    //TRANSITION LOGIC

    public void OnStartClicked()
    {
        if (!isLevelLoading) StartCoroutine(TransitionToLevelSequence());
    }

    IEnumerator TransitionToLevelSequence()
    {
        isLevelLoading = true; 

        //before slide transition, we put the NEXT level's image onto the slider
        if (levelLoaderImage != null && levelBackgrounds.Length > 0)
        {
            levelLoaderImage.sprite = levelBackgrounds[0]; //set slider to Level 1 Image
            
            levelLoaderImage.color = Color.white; 
        }

        //fade out menu text
        StartCoroutine(FadeCanvasGroup(menuContentGroup, 0f, uiFadeDuration));

        //slide in new background (Right to Left)
        if (levelLoaderPanel != null)
        {
            levelLoaderPanel.gameObject.SetActive(true);
            if (biomeNameText != null) biomeNameText.text = "Traveling to: Midgard...";
            
            // Slide to center
            yield return StartCoroutine(SlidePanel(levelLoaderPanel, 0, loaderSlideDuration));
        }

        //show spinner
        yield return new WaitForSeconds(0.2f);
        if (loadingSpinner != null) loadingSpinner.SetActive(true);
        yield return new WaitForSeconds(2.0f); 

        //load scene
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (SceneManager.sceneCountInBuildSettings > nextSceneIndex)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.Log("End of Build. Resetting UI.");
            if (loadingSpinner != null) loadingSpinner.SetActive(false);
            
            // Slide back out
            yield return StartCoroutine(SlidePanel(levelLoaderPanel, Screen.width, loaderSlideDuration));
            levelLoaderPanel.gameObject.SetActive(false);
            
            // Fade Menu Back In
            StartCoroutine(FadeCanvasGroup(menuContentGroup, 1f, uiFadeDuration));
            
            isLevelLoading = false;
        }
    }

    //options logic
    public void ToggleOptions()
    {
        if (isLevelLoading) return;

        if (isOptionsOpen)
        {
            StartCoroutine(SlidePanel(optionsPanel, -optionsPanel.rect.width, optionsSlideDuration));
            StartCoroutine(FadeDimmer(false));
            //Fade menu text in
            StartCoroutine(FadeCanvasGroup(menuContentGroup, 1f, optionsSlideDuration)); 
            EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
        }
        else
        {
            StartCoroutine(SlidePanel(optionsPanel, 0, optionsSlideDuration));
            StartCoroutine(FadeDimmer(true));
            //Fade menu text OUT 
            StartCoroutine(FadeCanvasGroup(menuContentGroup, 0.3f, optionsSlideDuration));
            EventSystem.current.SetSelectedGameObject(closeOptionsButton.gameObject);
        }
        isOptionsOpen = !isOptionsOpen;
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
    {
        if (cg == null) yield break;
        float startAlpha = cg.alpha;
        float t = 0f;
        while(t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / duration);
            yield return null;
        }
        cg.alpha = targetAlpha;
    }

    IEnumerator FadeDimmer(bool fadeIn)
    {
        if (optionsDimmer == null) yield break;
        if (fadeIn) optionsDimmer.gameObject.SetActive(true);
        float t = 0f;
        while (t < optionsSlideDuration)
        {
            t += Time.deltaTime;
            optionsDimmer.alpha = fadeIn ? t / optionsSlideDuration : 1f - (t / optionsSlideDuration);
            yield return null;
        }
        if (!fadeIn) optionsDimmer.gameObject.SetActive(false);
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
        Navigation startNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = loadGameButton, selectOnUp = settingsButton };
        startButton.navigation = startNav;
        Navigation loadNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = startButton, selectOnDown = settingsButton };
        loadGameButton.navigation = loadNav;
        Navigation settingsNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = loadGameButton, selectOnDown = startButton };
        settingsButton.navigation = settingsNav;
    }
}