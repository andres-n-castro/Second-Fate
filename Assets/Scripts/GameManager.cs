using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[Serializable]
public struct BonfireTravelData
{
    public string bonfireID;
    public string sceneName;
    public Vector2 spawnPosition;
}

[Serializable]
public struct BonfireLocation
{
    public string bonfireID;
    public string displayName;
    public string sceneName;
}

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Exploration,
        BossFight,
        Paused,
        InventoryMenu,
        BonfireMenu,
        ShopMenu,
        Respawning,
        Death
    }

    public enum AlignmentType
    {
        None,
        TreeEssence,
        CreatureBlood
    }

    public static GameManager Instance;
    public static event Action<GameState> OnStateChanged;
    public static event Action OnDashUnlocked;
    public static event Action OnPlayerDied;
    public event Action OnWorldReset;

    public GameState currentState { get; private set; } = GameState.Exploration;

    private GameState previousState;

    public Vector2 currentRespawnPoint;
    public string lastInteractedBonfireID;
    public string lastRestedBonfireID;

    [Header("Fast Travel Registry")]
    public List<BonfireLocation> masterBonfireList = new List<BonfireLocation>();
    public string pendingTeleportBonfireID = "";

    public List<BonfireTravelData> masterBonfireRegistry;
    public List<string> unlockedBonfires = new List<string>();

    private Dictionary<string, AlignmentType> imbuedBonfires = new Dictionary<string, AlignmentType>();

    public float globalGoodMultiplier { get; private set; } = 1.0f;
    public float globalBadMultiplier { get; private set; } = 1.0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.LoadGame(0);
        }

        Bonfire[] bonfires = UnityEngine.Object.FindFirstObjectByType<Bonfire>() != null
            ? UnityEngine.Object.FindObjectsByType<Bonfire>(FindObjectsSortMode.None)
            : new Bonfire[0];

        foreach (Bonfire b in bonfires)
        {
            b.UpdateVisualState();
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void ChangeState(GameState newState)
    {
        if (currentState == GameState.Respawning)
        {
            return;
        }

        previousState = currentState;
        currentState = newState;

        switch (newState)
        {
            case GameState.Paused:
            case GameState.InventoryMenu:
            case GameState.BonfireMenu:
            case GameState.ShopMenu:
                Time.timeScale = 0f;
                break;
            case GameState.Exploration:
            case GameState.BossFight:
            case GameState.Death:
                Time.timeScale = 1f;
                break;
        }

        OnStateChanged?.Invoke(currentState);
    }

    public void RestorePreviousState()
    {
        if (currentState == GameState.Respawning)
        {
            return;
        }

        ChangeState(previousState);
    }

    public void HandlePlayerDeath()
    {
        StartCoroutine(RespawnSequence());
    }

    public void UnlockDashAbility()
    {
        OnDashUnlocked?.Invoke();
    }

    public void TriggerDeathMenu()
    {
        OnPlayerDied?.Invoke();
        ChangeState(GameState.Death);
    }

    public void TriggerWorldReset()
    {
        OnWorldReset?.Invoke();
    }

    public void RetryFromCheckpoint()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        ChangeState(GameState.Exploration);
    }

    private IEnumerator RespawnSequence()
    {
        currentState = GameState.Respawning;
        OnStateChanged?.Invoke(currentState);

        yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));

        PlayerController.Instance.transform.position = currentRespawnPoint;
        PlayerManager.Instance.playerStats.currentHealth = PlayerManager.Instance.playerStats.maxHealth;
        PlayerManager.Instance.playerStats.SyncHealthForSaving(PlayerManager.Instance.playerStats.maxHealth, PlayerManager.Instance.playerStats.maxHealth);

        if (PlayerManager.Instance.playerStats.playerHealthComponent != null)
        {
            PlayerManager.Instance.playerStats.playerHealthComponent.InitializeHealth(
                PlayerManager.Instance.playerStats.maxHealth,
                PlayerManager.Instance.playerStats.maxHealth);
        }

        PlayerManager.Instance.playerStates.isDead = false;

        yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));
        // TODO: Trigger the World Reset logic (respawning non-boss enemies).

        currentState = GameState.Exploration;
        Time.timeScale = 1f;
        OnStateChanged?.Invoke(currentState);
    }

    public AlignmentType GetBonfireAlignment(string bonfireID)
    {
        if (imbuedBonfires.TryGetValue(bonfireID, out AlignmentType alignment))
        {
            return alignment;
        }

        return AlignmentType.None;
    }

    public AlignmentType GetActiveAlignment()
    {
        if (string.IsNullOrEmpty(lastInteractedBonfireID))
        {
            return AlignmentType.None;
        }

        return GetBonfireAlignment(lastInteractedBonfireID);
    }

    public void UnlockBonfire(string bonfireID)
    {
        if (!unlockedBonfires.Contains(bonfireID))
        {
            unlockedBonfires.Add(bonfireID);
        }
    }

    public List<string> GetImbuedBonfireIDs()
    {
        return new List<string>(imbuedBonfires.Keys);
    }

    public List<int> GetImbuedBonfireAlignments()
    {
        List<int> alignments = new List<int>();
        foreach (AlignmentType alignment in imbuedBonfires.Values)
        {
            alignments.Add((int)alignment);
        }

        return alignments;
    }

    public void ClearBonfireSaveState()
    {
        unlockedBonfires.Clear();
        imbuedBonfires.Clear();
        globalGoodMultiplier = 1.0f;
        globalBadMultiplier = 1.0f;
    }

    public void FastTravelTo(string bonfireID)
    {
        foreach (BonfireLocation location in masterBonfireList)
        {
            if (location.bonfireID == bonfireID)
            {
                FastTravelTo(bonfireID, location.sceneName);
                return;
            }
        }
    }

    public void FastTravelTo(string bonfireID, string targetSceneName)
    {
        pendingTeleportBonfireID = bonfireID;
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.currentSaveData.lastRestedBonfireID = bonfireID;
            SaveManager.Instance.SaveGame(0);
        }

        SceneManager.LoadScene(targetSceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.IsNullOrEmpty(pendingTeleportBonfireID))
        {
            Bonfire[] bonfires = FindObjectsByType<Bonfire>(FindObjectsSortMode.None);
            foreach (Bonfire b in bonfires)
            {
                if (b.bonfireID == pendingTeleportBonfireID)
                {
                    if (PlayerManager.Instance != null)
                    {
                        PlayerManager.Instance.transform.position = b.transform.position;
                        currentRespawnPoint = b.transform.position;
                        lastInteractedBonfireID = b.bonfireID;
                        lastRestedBonfireID = b.bonfireID;

                        if (SaveManager.Instance != null)
                        {
                            SaveManager.Instance.currentSaveData.lastRestedBonfireID = b.bonfireID;
                            SaveManager.Instance.SaveGame(0);
                        }
                    }
                    break;
                }
            }

            pendingTeleportBonfireID = "";
            TriggerWorldReset();
        }
    }

    public void SetBonfireAlignment(string bonfireID, AlignmentType type)
    {
        imbuedBonfires[bonfireID] = type;

        int goodCount = 0;
        int badCount = 0;

        foreach (AlignmentType alignment in imbuedBonfires.Values)
        {
            switch (alignment)
            {
                case AlignmentType.TreeEssence:
                    goodCount++;
                    break;
                case AlignmentType.CreatureBlood:
                    badCount++;
                    break;
            }
        }

        globalGoodMultiplier = 1.0f + (goodCount * 0.1f);
        globalBadMultiplier = 1.0f + (badCount * 0.1f);
    }
}
