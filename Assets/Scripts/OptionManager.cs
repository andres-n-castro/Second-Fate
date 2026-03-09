using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;

public class OptionsManager : MonoBehaviour
{
    [Header("Options Panel")]
    public RectTransform optionsPanel;
    public Button closeOptionsButton;
    public Button resumeButton;
    public Button mainMenuButton;
    public CanvasGroup optionsDimmer;
    public float optionsSlideDuration = 0.4f;
    private bool isOptionsOpen = false;

    [Header("Settings (For Later)")]
    public GameObject volumeSlider;
    public GameObject brightnessSlider;

    void Start()
    {
        
        // Set position FIRST before anything else
        if (optionsPanel != null)
            optionsPanel.anchoredPosition = new Vector2(-Screen.width, 40f);

        if (optionsDimmer != null)
        {
            optionsDimmer.alpha = 0;
            optionsDimmer.gameObject.SetActive(false);
        }

        // Setup buttons
        if (closeOptionsButton != null)
        {
            closeOptionsButton.onClick.RemoveAllListeners();
            closeOptionsButton.onClick.AddListener(CloseOptions);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(CloseOptions);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOptionsOpen)
                CloseOptions();
            else
                OpenOptions();
        }

        if (isOptionsOpen && Input.GetKeyDown(KeyCode.Space))
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null)
            {
                Button button = selected.GetComponent<Button>();
                if (button != null) button.onClick.Invoke();
            }
        }
    }
    public void OpenOptions()
    {
        if (isOptionsOpen) return;

        isOptionsOpen = true;

        Time.timeScale = 0f;

        StartCoroutine(SlidePanel(optionsPanel, 0, 40f,optionsSlideDuration));
        StartCoroutine(FadeDimmer(true));

        if (closeOptionsButton != null)
            EventSystem.current.SetSelectedGameObject(closeOptionsButton.gameObject);
    }

    public void CloseOptions()
    {
        if (!isOptionsOpen) return;

        isOptionsOpen = false;
        StartCoroutine(CloseOptionsSequence());
    }
    IEnumerator CloseOptionsSequence()
    {
        Time.timeScale = 1f;
        StartCoroutine(SlidePanel(optionsPanel, -Screen.width, 40f, optionsSlideDuration));
        StartCoroutine(FadeDimmer(false));
        yield return new WaitForSecondsRealtime(optionsSlideDuration);
    }
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // Ensure time is running before loading main menu
        SceneManager.LoadScene("main_menu_scene");
    }

    IEnumerator FadeDimmer(bool fadeIn)
    {
        if (optionsDimmer == null) yield break;
        if (fadeIn) optionsDimmer.gameObject.SetActive(true);

        float t = 0f;
        while (t < optionsSlideDuration)
        {
            t += Time.unscaledDeltaTime;
            optionsDimmer.alpha = fadeIn ? t / optionsSlideDuration : 1f - (t / optionsSlideDuration);
            yield return null;
        }

        optionsDimmer.alpha = fadeIn ? 1f : 0f;
        if (!fadeIn) optionsDimmer.gameObject.SetActive(false);
    }
    IEnumerator SlidePanel(RectTransform panel, float targetX, float targetY, float duration)
    {
        float t = 0f;
        Vector2 start = panel.anchoredPosition;
        Vector2 end = new Vector2(targetX, targetY);

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, t / duration);
            panel.anchoredPosition = Vector2.Lerp(start, end, smooth);
            yield return null;
        }

        panel.anchoredPosition = end;
    }
}