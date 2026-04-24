using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class BossUIManager : MonoBehaviour
{
    public static BossUIManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject bossHudPanel;
    public TextMeshProUGUI bossNameText;
    public Image healthBarFill;

    [Header("Boss Death UI")]
    public GameObject bossDeathPopUp;
    public TextMeshProUGUI deathDescriptionText;
    public Button resumeButton;

    [Header("Boss Rewards")]
    public Item treeEssenceReward;
    public Item creatureBloodReward;

    private int currentBossMaxHealth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Hide the HUD before the first rendered frame so it doesn't flash on scene load.
        if (bossHudPanel != null)
        {
            bossHudPanel.SetActive(false);
        }

        if (bossDeathPopUp != null)
        {
            bossDeathPopUp.SetActive(false);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(ResumeGame);
            resumeButton.onClick.AddListener(ResumeGame);
        }
    }

    private void OnDestroy()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(ResumeGame);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void InitializeBossFight(string bossName, int maxHealth)
    {
        CancelInvoke(nameof(HideBossUI));

        currentBossMaxHealth = maxHealth;

        if (bossNameText != null)
        {
            bossNameText.text = bossName;
        }

        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = 1f;
        }

        if (bossHudPanel != null)
        {
            bossHudPanel.SetActive(true);
        }
    }

    public void UpdateBossHealth(int currentHealth)
    {
        if (healthBarFill != null && currentBossMaxHealth > 0)
        {
            float healthPercent = (float)currentHealth / currentBossMaxHealth;
            healthBarFill.fillAmount = Mathf.Clamp01(healthPercent);
        }

        if (currentHealth <= 0)
        {
            CancelInvoke(nameof(HideBossUI));
            Invoke(nameof(HideBossUI), 2f);
        }
    }

    public void TriggerBossDeath(string customDeathText)
    {
        CancelInvoke(nameof(HideBossUI));
        HideBossUI();

        if (deathDescriptionText != null)
        {
            deathDescriptionText.text = customDeathText;
        }

        GrantReward(treeEssenceReward);
        GrantReward(creatureBloodReward);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("Blood and Essence Recovered");
        }

        // Only freeze the game if we actually have a popup/resume button to un-freeze it.
        // Without them the player would be softlocked.
        bool canShowPopup = bossDeathPopUp != null && resumeButton != null;
        if (!canShowPopup)
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveGame(0);
            }
            return;
        }

        bossDeathPopUp.SetActive(true);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.SetExternalFreeze(true);
        }

        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.SetExternalFreeze(false);
        }

        Time.timeScale = 1f;

        if (bossDeathPopUp != null)
        {
            bossDeathPopUp.SetActive(false);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame(0);
        }
    }

    private void HideBossUI()
    {
        currentBossMaxHealth = 0;

        if (bossHudPanel != null)
        {
            bossHudPanel.SetActive(false);
        }
    }

    private void GrantReward(Item reward)
    {
        if (reward == null) return;
        if (InventoryController.Instance == null || InventoryController.Instance.inventoryModel == null) return;

        InventoryController.Instance.inventoryModel.AddItem(reward);
    }
}
