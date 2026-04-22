using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public static event Action OnInventoryToggled;

    public GameObject playerHUDCanvas;
    public GameObject inventoryCanvas;

    [Header("Pause Menu")]
    public GameObject pauseCanvas;
    public GameObject resumeButtonObject;

    public GameObject bonfireCanvas;
    public GameObject deathCanvas;

    [Header("Death Menu")]
    public GameObject deathMenuPanel;
    public GameObject retryButtonObject;

    public GameObject abilityUnlockPanel;
    public Image abilityIcon;
    public TextMeshProUGUI abilityDescriptionText;
    public CanvasGroup fadeScreenGroup;

    [Header("Item Notifications")]
    public CanvasGroup notificationGroup;
    public TextMeshProUGUI notificationText;
    private Coroutine notificationCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (fadeScreenGroup != null)
        {
            fadeScreenGroup.alpha = 0f;
        }

        if (notificationGroup != null)
        {
            notificationGroup.alpha = 0f;
            notificationGroup.interactable = false;
            notificationGroup.blocksRaycasts = false;
        }

        if (abilityUnlockPanel != null)
        {
            abilityUnlockPanel.SetActive(false);
        }

        if (deathMenuPanel != null)
        {
            deathMenuPanel.SetActive(false);
        }

        if (GameManager.Instance != null)
        {
            HandleGameStateChange(GameManager.Instance.currentState);
        }
    }

    void OnEnable()
    {
        GameManager.OnStateChanged += HandleGameStateChange;
        GameManager.OnDashUnlocked += ShowDashUnlockedUI;
        GameManager.OnPlayerDied += ShowDeathMenu;
    }

    void OnDisable()
    {
        GameManager.OnStateChanged -= HandleGameStateChange;
        GameManager.OnDashUnlocked -= ShowDashUnlockedUI;
        GameManager.OnPlayerDied -= ShowDeathMenu;
    }

    private void HandleGameStateChange(GameManager.GameState state)
    {
        HideNotification();
        SetAllCanvasesInactive();

        switch (state)
        {
            case GameManager.GameState.Exploration:
            case GameManager.GameState.BossFight:
                if (playerHUDCanvas != null) playerHUDCanvas.SetActive(true);
                break;
            case GameManager.GameState.InventoryMenu:
                if (inventoryCanvas != null) inventoryCanvas.SetActive(true);
                OnInventoryToggled?.Invoke();
                break;
            case GameManager.GameState.Paused:
                if (pauseCanvas != null) pauseCanvas.SetActive(true);
                if (EventSystem.current != null && resumeButtonObject != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(resumeButtonObject);
                }
                break;
            case GameManager.GameState.BonfireMenu:
                if (bonfireCanvas != null) bonfireCanvas.SetActive(true);
                break;
            case GameManager.GameState.Respawning:
                if (deathCanvas != null) deathCanvas.SetActive(true);
                break;
            case GameManager.GameState.Death:
                if (deathMenuPanel != null) deathMenuPanel.SetActive(true);
                break;
        }
    }

    private void SetAllCanvasesInactive()
    {
        if (playerHUDCanvas != null) playerHUDCanvas.SetActive(false);
        if (inventoryCanvas != null) inventoryCanvas.SetActive(false);
        if (pauseCanvas != null) pauseCanvas.SetActive(false);
        if (bonfireCanvas != null) bonfireCanvas.SetActive(false);
        if (deathCanvas != null) deathCanvas.SetActive(false);
        if (deathMenuPanel != null) deathMenuPanel.SetActive(false);
    }

    public IEnumerator FadeToBlack(float duration)
    {
        if (fadeScreenGroup == null)
        {
            yield break;
        }

        float elapsedTime = 0f;
        float startAlpha = fadeScreenGroup.alpha;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            fadeScreenGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsedTime / duration);
            yield return null;
        }

        fadeScreenGroup.alpha = 1f;
    }

    public IEnumerator FadeToClear(float duration)
    {
        if (fadeScreenGroup == null)
        {
            yield break;
        }

        float elapsedTime = 0f;
        float startAlpha = fadeScreenGroup.alpha;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            fadeScreenGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsedTime / duration);
            yield return null;
        }

        fadeScreenGroup.alpha = 0f;
    }

    public void ShowNotification(string message)
    {
        if (notificationGroup == null || notificationText == null) return;

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }

        notificationCoroutine = StartCoroutine(FadeNotification(message));
    }

    private IEnumerator FadeNotification(string message)
    {
        notificationText.text = message;
        notificationGroup.alpha = 0f;
        notificationGroup.interactable = false;
        notificationGroup.blocksRaycasts = false;

        while (notificationGroup.alpha < 1f)
        {
            notificationGroup.alpha += Time.unscaledDeltaTime * 4f;
            yield return null;
        }

        notificationGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(2f);

        while (notificationGroup.alpha > 0f)
        {
            notificationGroup.alpha -= Time.unscaledDeltaTime * 2f;
            yield return null;
        }

        notificationGroup.alpha = 0f;
        notificationGroup.interactable = false;
        notificationGroup.blocksRaycasts = false;
        notificationCoroutine = null;
    }

    private void HideNotification()
    {
        if (notificationGroup == null)
        {
            return;
        }

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
            notificationCoroutine = null;
        }

        notificationGroup.alpha = 0f;
        notificationGroup.interactable = false;
        notificationGroup.blocksRaycasts = false;
    }

    private void ShowDashUnlockedUI()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.playerStats != null)
        {
            PlayerManager.Instance.playerStats.canDash = true;
        }

        if (abilityDescriptionText != null)
        {
            abilityDescriptionText.text = "Dash unlocked! Press Shift or Circle to dash.";
        }

        if (abilityUnlockPanel != null)
        {
            abilityUnlockPanel.SetActive(true);
        }

        Time.timeScale = 0f;
    }

    public void CloseAbilityUI()
    {
        if (abilityUnlockPanel != null)
        {
            abilityUnlockPanel.SetActive(false);
        }

        Time.timeScale = 1f;
    }

    private void ShowDeathMenu()
    {
        if (deathMenuPanel != null)
        {
            deathMenuPanel.SetActive(true);
        }

        if (EventSystem.current != null && retryButtonObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(retryButtonObject);
        }
    }

    public void OnRetryClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RetryFromCheckpoint();
        }

        if (deathMenuPanel != null)
        {
            deathMenuPanel.SetActive(false);
        }
    }
}
