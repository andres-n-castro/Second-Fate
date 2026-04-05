using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public static event Action OnInventoryToggled;

    public GameObject playerHUDCanvas;
    public GameObject inventoryCanvas;
    public GameObject pauseCanvas;
    public GameObject bonfireCanvas;
    public GameObject deathCanvas;
    public GameObject abilityUnlockPanel;
    public Image abilityIcon;
    public TextMeshProUGUI abilityDescriptionText;
    public CanvasGroup fadeScreenGroup;

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

        if (abilityUnlockPanel != null)
        {
            abilityUnlockPanel.SetActive(false);
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
    }

    void OnDisable()
    {
        GameManager.OnStateChanged -= HandleGameStateChange;
        GameManager.OnDashUnlocked -= ShowDashUnlockedUI;
    }

    private void HandleGameStateChange(GameManager.GameState state)
    {
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
                break;
            case GameManager.GameState.BonfireMenu:
                if (bonfireCanvas != null) bonfireCanvas.SetActive(true);
                break;
            case GameManager.GameState.Respawning:
                if (deathCanvas != null) deathCanvas.SetActive(true);
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
}
