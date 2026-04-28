using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BossUIManager : MonoBehaviour
{
    public static BossUIManager Instance { get; private set; }
    private const string TreeEssenceItemID = "Tree Essence";
    private const string CreatureBloodItemID = "Creature Blood";

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
        AutoBindReferences();

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
        AutoBindReferences();
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
        AutoBindReferences();
        CancelInvoke(nameof(HideBossUI));
        HideBossUI();

        if (deathDescriptionText != null)
        {
            deathDescriptionText.text = customDeathText;
        }

        GrantReward(ResolveReward(treeEssenceReward, TreeEssenceItemID));
        GrantReward(ResolveReward(creatureBloodReward, CreatureBloodItemID));

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveCurrentSlot();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("Blood and Essence Recovered");
        }

        // Only freeze the game if we actually have a popup/resume button to un-freeze it.
        // Without them the player would be softlocked.
        bool canShowPopup = bossDeathPopUp != null && resumeButton != null;
        if (!canShowPopup)
        {
            return;
        }

        PrepareBossDeathPopUpForDisplay();
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
        if (InventoryController.Instance == null || InventoryController.Instance.inventoryModel == null)
        {
            AddRewardToSaveData(reward);
            return;
        }

        InventoryController.Instance.inventoryModel.AddItem(reward);

        if (InventoryController.Instance.inventoryView != null)
        {
            InventoryController.Instance.inventoryView.DrawInventory();
        }
    }

    private void AddRewardToSaveData(Item reward)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.currentSaveData == null) return;

        GameData saveData = SaveManager.Instance.currentSaveData;
        int existingIndex = saveData.inventoryItemIDs.IndexOf(reward.name);
        if (existingIndex >= 0)
        {
            while (saveData.inventoryItemAmounts.Count <= existingIndex)
            {
                saveData.inventoryItemAmounts.Add(1);
            }

            saveData.inventoryItemAmounts[existingIndex] += 1;
            return;
        }

        saveData.inventoryItemIDs.Add(reward.name);
        saveData.inventoryItemAmounts.Add(1);
        saveData.inventoryItemReadStates.Add(false);
    }

    private Item ResolveReward(Item serializedReward, string itemID)
    {
        if (serializedReward != null)
        {
            return serializedReward;
        }

        if (GameDatabase.Instance == null)
        {
            return null;
        }

        return GameDatabase.Instance.GetItemByID(itemID);
    }

    private void AutoBindReferences()
    {
        if (bossHudPanel == null)
        {
            bossHudPanel = gameObject;
        }

        if (bossNameText == null || healthBarFill == null)
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            Image[] images = GetComponentsInChildren<Image>(true);

            if (bossNameText == null)
            {
                bossNameText = FindTextByName(texts, "BossName");
            }

            if (healthBarFill == null)
            {
                healthBarFill = FindImageByName(images, "HealthFill");
            }
        }

        if (bossDeathPopUp == null)
        {
            bossDeathPopUp = FindSceneObject("BossDeathPopUp");
        }

        if (bossDeathPopUp != null && (deathDescriptionText == null || resumeButton == null))
        {
            TextMeshProUGUI[] popupTexts = bossDeathPopUp.GetComponentsInChildren<TextMeshProUGUI>(true);
            Button[] popupButtons = bossDeathPopUp.GetComponentsInChildren<Button>(true);

            if (deathDescriptionText == null)
            {
                deathDescriptionText = FindTextByName(popupTexts, "DeathDescription")
                    ?? FindTextByName(popupTexts, "EnemyDeadText")
                    ?? FindFirst(popupTexts);
            }

            if (resumeButton == null)
            {
                resumeButton = FindButtonByName(popupButtons, "Close Button") ?? FindFirst(popupButtons);
            }
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(ResumeGame);
            resumeButton.onClick.AddListener(ResumeGame);
        }
    }

    private void PrepareBossDeathPopUpForDisplay()
    {
        if (bossDeathPopUp == null)
        {
            return;
        }

        RectTransform popupRect = bossDeathPopUp.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            popupRect.localScale = Vector3.one;
            popupRect.anchoredPosition3D = Vector3.zero;
        }

        Canvas popupCanvas = bossDeathPopUp.GetComponent<Canvas>();
        if (popupCanvas != null)
        {
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = 500;
        }
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate == null || string.IsNullOrEmpty(candidate.scene.name))
            {
                continue;
            }

            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static TextMeshProUGUI FindTextByName(IEnumerable<TextMeshProUGUI> texts, string objectName)
    {
        foreach (TextMeshProUGUI text in texts)
        {
            if (text != null && text.gameObject.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private static Image FindImageByName(IEnumerable<Image> images, string objectName)
    {
        foreach (Image image in images)
        {
            if (image != null && image.gameObject.name == objectName)
            {
                return image;
            }
        }

        return null;
    }

    private static Button FindButtonByName(IEnumerable<Button> buttons, string objectName)
    {
        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private static T FindFirst<T>(IEnumerable<T> items) where T : Component
    {
        foreach (T item in items)
        {
            if (item != null)
            {
                return item;
            }
        }

        return null;
    }
}
