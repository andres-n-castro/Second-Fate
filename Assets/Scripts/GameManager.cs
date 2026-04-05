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

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Exploration,
        BossFight,
        Paused,
        InventoryMenu,
        BonfireMenu,
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

    public GameState currentState { get; private set; } = GameState.Exploration;

    private GameState previousState;

    public Vector2 currentRespawnPoint;
    public string lastInteractedBonfireID;
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

    public void UnlockBonfire(string bonfireID)
    {
        if (!unlockedBonfires.Contains(bonfireID))
        {
            unlockedBonfires.Add(bonfireID);
        }
    }

    public void FastTravelTo(string bonfireID)
    {
        StartCoroutine(FastTravelSequence(bonfireID));
    }

    private IEnumerator FastTravelSequence(string bonfireID)
    {
        bool foundTarget = false;
        BonfireTravelData targetData = default;

        foreach (BonfireTravelData bonfireData in masterBonfireRegistry)
        {
            if (bonfireData.bonfireID == bonfireID)
            {
                targetData = bonfireData;
                foundTarget = true;
                break;
            }
        }

        if (!foundTarget)
        {
            yield break;
        }

        currentState = GameState.Respawning;
        OnStateChanged?.Invoke(currentState);

        yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));

        if (SceneManager.GetActiveScene().name != targetData.sceneName)
        {
            yield return SceneManager.LoadSceneAsync(targetData.sceneName);
        }

        PlayerController.Instance.transform.position = targetData.spawnPosition;
        currentRespawnPoint = targetData.spawnPosition;

        yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));

        currentState = GameState.Exploration;
        Time.timeScale = 1f;
        OnStateChanged?.Invoke(currentState);
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
