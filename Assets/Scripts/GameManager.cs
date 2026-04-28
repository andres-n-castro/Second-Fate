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
    private const string TutorialFallbackCheckpointName = "Checkpoint_Midpoint01";
    private const string TutorialSceneName = "tutorial_hub";
    private const string MainMenuSceneName = "main_menu_scene";

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

    public enum FinalBossType
    {
        Odin,
        Heimdall
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

    [Header("Boss HUD")]
    [SerializeField] private GameObject bossHudPrefab;

    [Header("Final Boss Inspector Testing")]
    [SerializeField] private bool useInspectorFinalBossCounts;
    [SerializeField] private int inspectorTreeEssenceCount;
    [SerializeField] private int inspectorCreatureBloodCount;

    [Header("Fast Travel Registry")]
    public List<BonfireLocation> masterBonfireList = new List<BonfireLocation>();
    public string pendingTeleportBonfireID = "";

    // Used for checkpoint respawn (tutorial scene) across a scene reload.
    private bool hasPendingCheckpointRespawn;
    private Vector2 pendingCheckpointPosition;
    private bool placePlayerFromSaveOnNextGameplayLoad;
    private bool isLoadedGamePlacementRunning;

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
        RefreshSceneBonfires();

        ConfigureSceneBosses(SceneManager.GetActiveScene());
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

        // No rested bonfire yet: use the tutorial checkpoint if available.
        if (!LastRestedBonfireExistsInActiveScene() && TryGetCheckpointRespawnPosition(out Vector2 checkpointPosition))
        {
            RevivePlayerAt(checkpointPosition);
            currentRespawnPoint = checkpointPosition;
            TriggerWorldReset();
            ChangeState(GameState.Exploration);
            return;
        }

        if (!string.IsNullOrEmpty(lastRestedBonfireID))
        {
            // FastTravelTo handles scene loading (including reloading the current scene)
            // and positions the player at the bonfire via OnSceneLoaded.
            FastTravelTo(lastRestedBonfireID);
            ChangeState(GameState.Exploration);
            return;
        }

        // Fallback: just reload the current scene.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        ChangeState(GameState.Exploration);
    }

    private bool TryGetCheckpointRespawnPosition(out Vector2 checkpointPosition)
    {
        checkpointPosition = Vector2.zero;

        if (PlayerController.Instance != null
            && PlayerController.Instance.gameObject.scene == SceneManager.GetActiveScene())
        {
            PlayerRespawn respawnScript = PlayerController.Instance.GetComponent<PlayerRespawn>();
            if (respawnScript != null && respawnScript.useCheckpointRespawn && respawnScript.currentCheckpoint != null)
            {
                checkpointPosition = respawnScript.currentCheckpoint.position;
                return true;
            }
        }

        GameObject fallbackCheckpoint = FindSceneObjectByName(TutorialFallbackCheckpointName);
        if (fallbackCheckpoint != null)
        {
            checkpointPosition = fallbackCheckpoint.transform.position;
            return true;
        }

        return false;
    }

    private bool LastRestedBonfireExistsInActiveScene()
    {
        if (string.IsNullOrEmpty(lastRestedBonfireID))
        {
            return false;
        }

        Bonfire[] bonfires = FindObjectsByType<Bonfire>(FindObjectsSortMode.None);
        for (int i = 0; i < bonfires.Length; i++)
        {
            if (bonfires[i] != null
                && (bonfires[i].SaveID == lastRestedBonfireID || bonfires[i].bonfireID == lastRestedBonfireID))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator RespawnSequence()
    {
        currentState = GameState.Respawning;
        OnStateChanged?.Invoke(currentState);

        yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));

        // Pick respawn position: checkpoint (first scene only) or last rested bonfire.
        Vector2 respawnPos = currentRespawnPoint;
        PlayerRespawn respawnScript = PlayerController.Instance.GetComponent<PlayerRespawn>();
        if (respawnScript != null
            && respawnScript.useCheckpointRespawn
            && respawnScript.currentCheckpoint != null)
        {
            respawnPos = respawnScript.currentCheckpoint.position;
        }

        RevivePlayerAt(respawnPos);

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

    public static string GetBonfireSaveID(string sceneName, string bonfireID)
    {
        if (string.IsNullOrWhiteSpace(bonfireID))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(sceneName) || bonfireID.Contains(":")
            ? bonfireID
            : $"{sceneName}:{bonfireID}";
    }

    public AlignmentType GetActiveAlignment()
    {
        if (string.IsNullOrEmpty(lastInteractedBonfireID))
        {
            return AlignmentType.None;
        }

        return GetBonfireAlignment(lastInteractedBonfireID);
    }

    public FinalBossType DetermineFinalBoss()
    {
        GetFinalBossAlignmentCounts(out int essenceCount, out int bloodCount);

        if (essenceCount >= bloodCount)
        {
            Debug.Log($"Final Boss Decided: Odin (Tree Essence path) | Tree Essence={essenceCount}, Creature Blood={bloodCount}");
            return FinalBossType.Odin;
        }

        Debug.Log($"Final Boss Decided: Heimdall (Creature Blood path) | Tree Essence={essenceCount}, Creature Blood={bloodCount}");
        return FinalBossType.Heimdall;
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
            string locationSaveID = GetBonfireSaveID(location.sceneName, location.bonfireID);
            if (location.bonfireID == bonfireID || locationSaveID == bonfireID)
            {
                FastTravelTo(locationSaveID, location.sceneName);
                return;
            }
        }
    }

    public void FastTravelTo(string bonfireID, string targetSceneName)
    {
        pendingTeleportBonfireID = GetBonfireSaveID(targetSceneName, bonfireID);
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.currentSaveData.lastRestedBonfireID = pendingTeleportBonfireID;
            SaveManager.Instance.SaveCurrentSlot();
        }

        SceneManager.LoadScene(targetSceneName);
    }

    public void QueueLoadedGamePlacement()
    {
        placePlayerFromSaveOnNextGameplayLoad = true;
    }

    public void RefreshSceneBonfires()
    {
        Bonfire[] bonfires = UnityEngine.Object.FindFirstObjectByType<Bonfire>() != null
            ? UnityEngine.Object.FindObjectsByType<Bonfire>(FindObjectsSortMode.None)
            : new Bonfire[0];

        foreach (Bonfire b in bonfires)
        {
            if (b != null)
            {
                b.UpdateVisualState();
            }
        }
    }

    public void PlaceLoadedGamePlayer()
    {
        isLoadedGamePlacementRunning = true;
        StartCoroutine(PlaceLoadedPlayerAfterSceneLoad());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameManager] Scene loaded: '{scene.name}' mode={mode}. Active scene='{SceneManager.GetActiveScene().name}'. placeLoaded={placePlayerFromSaveOnNextGameplayLoad}, pendingCheckpoint={hasPendingCheckpointRespawn}, pendingBonfire='{pendingTeleportBonfireID}'.");

        if (IsGameplayScene(scene.name))
        {
            ResetStateForGameplayScene();
        }

        ConfigureSceneBosses(scene);
        RefreshSceneBonfires();

        if (ShouldPlaceTutorialPlayerAtCheckpoint(scene))
        {
            StartCoroutine(PlaceTutorialPlayerAtCheckpointAfterSceneLoad());
        }

        if (hasPendingCheckpointRespawn)
        {
            StartCoroutine(RevivePlayerAtCheckpointAfterSceneLoad(pendingCheckpointPosition));
            hasPendingCheckpointRespawn = false;
        }

        if (placePlayerFromSaveOnNextGameplayLoad && IsGameplayScene(scene.name))
        {
            StartCoroutine(PlaceLoadedPlayerAfterSceneLoad());
            placePlayerFromSaveOnNextGameplayLoad = false;
        }

        if (!string.IsNullOrEmpty(pendingTeleportBonfireID))
        {
            Bonfire[] bonfires = FindObjectsByType<Bonfire>(FindObjectsSortMode.None);
            foreach (Bonfire b in bonfires)
            {
                if (b.SaveID == pendingTeleportBonfireID || b.bonfireID == pendingTeleportBonfireID)
                {
                    if (PlayerManager.Instance != null)
                    {
                        RevivePlayerAt(b.transform.position);
                        currentRespawnPoint = b.transform.position;
                        lastInteractedBonfireID = b.SaveID;
                        lastRestedBonfireID = b.SaveID;

                        if (SaveManager.Instance != null)
                        {
                            SaveManager.Instance.currentSaveData.lastRestedBonfireID = b.SaveID;
                            SaveManager.Instance.SaveCurrentSlot();
                        }
                    }
                    break;
                }
            }

            pendingTeleportBonfireID = "";
            TriggerWorldReset();
        }
    }

    private void ResetStateForGameplayScene()
    {
        previousState = GameState.Exploration;
        currentState = GameState.Exploration;
        Time.timeScale = 1f;
        OnStateChanged?.Invoke(currentState);
    }

    private bool IsGameplayScene(string sceneName)
    {
        return !string.Equals(sceneName, MainMenuSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldPlaceTutorialPlayerAtCheckpoint(Scene scene)
    {
        return string.Equals(scene.name, TutorialSceneName, StringComparison.OrdinalIgnoreCase)
            && !hasPendingCheckpointRespawn
            && !isLoadedGamePlacementRunning
            && string.IsNullOrEmpty(pendingTeleportBonfireID);
    }

    private IEnumerator PlaceTutorialPlayerAtCheckpointAfterSceneLoad()
    {
        Debug.Log("[GameManager] Tutorial checkpoint placement started.");

        for (int i = 0; i < 10 && GetScenePlayerManager() == null; i++)
        {
            yield return null;
        }

        if (TryGetCheckpointRespawnPosition(out Vector2 checkpointPosition))
        {
            Debug.Log($"[GameManager] Placing tutorial player at checkpoint {checkpointPosition}.");
            RevivePlayerAt(checkpointPosition);
            currentRespawnPoint = checkpointPosition;
        }
        else
        {
            Debug.LogWarning("[GameManager] Tutorial checkpoint placement failed because no checkpoint was found.");
        }
    }

    private IEnumerator RevivePlayerAtCheckpointAfterSceneLoad(Vector2 checkpointPosition)
    {
        for (int i = 0; i < 10 && GetScenePlayerManager() == null; i++)
        {
            yield return null;
        }

        RevivePlayerAt(checkpointPosition);
        currentRespawnPoint = checkpointPosition;
        TriggerWorldReset();
    }

    private IEnumerator PlaceLoadedPlayerAfterSceneLoad()
    {
        Debug.Log("[GameManager] Loaded-save player placement started.");

        // Let SaveManager apply loaded bonfire/player state before choosing a spawn point.
        yield return null;

        for (int i = 0; i < 10 && GetScenePlayerManager() == null; i++)
        {
            yield return null;
        }

        if (TryGetLastRestedBonfirePosition(out Vector2 bonfirePosition))
        {
            RevivePlayerAt(bonfirePosition);
            currentRespawnPoint = bonfirePosition;
            TriggerWorldReset();
            isLoadedGamePlacementRunning = false;
            yield break;
        }

        if (TryGetCheckpointRespawnPosition(out Vector2 checkpointPosition))
        {
            RevivePlayerAt(checkpointPosition);
            currentRespawnPoint = checkpointPosition;
            TriggerWorldReset();
        }

        isLoadedGamePlacementRunning = false;
    }

    private bool TryGetLastRestedBonfirePosition(out Vector2 bonfirePosition)
    {
        bonfirePosition = Vector2.zero;

        if (string.IsNullOrEmpty(lastRestedBonfireID))
        {
            return false;
        }

        Bonfire[] bonfires = FindObjectsByType<Bonfire>(FindObjectsSortMode.None);
        foreach (Bonfire bonfire in bonfires)
        {
            if (bonfire != null
                && (bonfire.SaveID == lastRestedBonfireID || bonfire.bonfireID == lastRestedBonfireID))
            {
                bonfirePosition = bonfire.transform.position;
                return true;
            }
        }

        return false;
    }

    private void RevivePlayerAt(Vector2 position)
    {
        PlayerManager playerManager = GetScenePlayerManager();
        if (playerManager == null)
        {
            Debug.LogWarning($"[GameManager] Could not revive player because no PlayerManager exists in scene '{SceneManager.GetActiveScene().name}'.");
            return;
        }

        PlayerManager.Instance = playerManager;
        PlayerController playerController = playerManager.GetComponent<PlayerController>();
        if (playerController != null)
        {
            PlayerController.Instance = playerController;
        }

        playerManager.gameObject.SetActive(true);
        playerManager.transform.position = position;

        Rigidbody2D rb = playerManager.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = true;
        }

        SpriteRenderer spriteRenderer = playerManager.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        Animator animator = playerManager.GetComponent<Animator>();
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        if (playerManager.playerController == null)
        {
            playerManager.playerController = playerController;
        }

        if (playerManager.playerMovement == null)
        {
            playerManager.playerMovement = playerManager.GetComponent<PlayerMovement>();
        }

        if (playerManager.playerStats == null)
        {
            playerManager.playerStats = playerManager.GetComponent<PlayerStats>();
        }

        if (playerManager.playerStates == null)
        {
            playerManager.playerStates = playerManager.GetComponent<PlayerStates>();
        }

        if (playerManager.playerController != null)
        {
            playerManager.playerController.enabled = true;
            playerManager.playerController.SetExternalFreeze(false);
        }

        if (playerManager.playerMovement != null)
        {
            playerManager.playerMovement.enabled = true;
        }

        PlayerAttack playerAttack = playerManager.GetComponent<PlayerAttack>();
        if (playerAttack != null)
        {
            playerAttack.enabled = true;
            playerAttack.DeactivateAllHitboxes();
        }

        if (playerManager.playerStates != null)
        {
            playerManager.playerStates.isDead = false;
            playerManager.playerStates.isInvincible = false;
        }

        if (playerManager.playerStats != null)
        {
            playerManager.playerStats.currentHealth = playerManager.playerStats.maxHealth;
            playerManager.playerStats.SyncHealthForSaving(playerManager.playerStats.maxHealth, playerManager.playerStats.maxHealth);

            if (playerManager.playerStats.playerHealthComponent != null)
            {
                playerManager.playerStats.playerHealthComponent.InitializeHealth(
                    playerManager.playerStats.maxHealth,
                    playerManager.playerStats.maxHealth);
                playerManager.playerStats.playerHealthComponent.isInvulnerable = false;
            }
        }

        ChangeState(GameState.Exploration);
    }

    private PlayerManager GetScenePlayerManager()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (PlayerManager.Instance != null && PlayerManager.Instance.gameObject.scene == activeScene)
        {
            Debug.Log($"[GameManager] Found PlayerManager from singleton on '{PlayerManager.Instance.name}' in scene '{activeScene.name}'.");
            return PlayerManager.Instance;
        }

        PlayerManager[] players = Resources.FindObjectsOfTypeAll<PlayerManager>();

        for (int i = 0; i < players.Length; i++)
        {
            PlayerManager candidate = players[i];
            if (candidate != null && candidate.gameObject.scene == activeScene)
            {
                Debug.Log($"[GameManager] Found PlayerManager by scene scan on '{candidate.name}' in scene '{activeScene.name}'.");
                return candidate;
            }
        }

        PlayerController playerController = FindSceneObjectOfType<PlayerController>(activeScene);
        if (playerController != null)
        {
            Debug.LogWarning($"[GameManager] Found PlayerController on '{playerController.name}' without PlayerManager in scene '{activeScene.name}'. Adding PlayerManager to the player.");
            return EnsurePlayerManager(playerController.gameObject);
        }

        GameObject taggedPlayer = FindSceneObjectWithTag(activeScene, "Player");
        if (taggedPlayer != null)
        {
            Debug.LogWarning($"[GameManager] Found tagged Player object '{taggedPlayer.name}' without PlayerManager in scene '{activeScene.name}'. Adding PlayerManager to the player.");
            return EnsurePlayerManager(taggedPlayer);
        }

        Debug.LogWarning($"[GameManager] Player scan failed in scene '{activeScene.name}'. PlayerManagers found: {players.Length}.");
        return null;
    }

    private PlayerManager EnsurePlayerManager(GameObject playerObject)
    {
        PlayerManager playerManager = playerObject.GetComponent<PlayerManager>();
        if (playerManager == null)
        {
            playerManager = playerObject.AddComponent<PlayerManager>();
        }

        PlayerManager.Instance = playerManager;
        return playerManager;
    }

    private GameObject FindSceneObjectWithTag(Scene scene, string tag)
    {
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject candidate = sceneObjects[i];
            if (candidate == null || candidate.scene != scene)
            {
                continue;
            }

            if (candidate.CompareTag(tag))
            {
                return candidate;
            }
        }

        return null;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        Scene activeScene = SceneManager.GetActiveScene();

        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject candidate = sceneObjects[i];
            if (candidate == null || candidate.scene != activeScene)
            {
                continue;
            }

            if (string.Equals(candidate.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
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

    private void GetFinalBossAlignmentCounts(out int essenceCount, out int bloodCount)
    {
        if (useInspectorFinalBossCounts)
        {
            essenceCount = Mathf.Max(0, inspectorTreeEssenceCount);
            bloodCount = Mathf.Max(0, inspectorCreatureBloodCount);
            return;
        }

        essenceCount = 0;
        bloodCount = 0;

        foreach (AlignmentType alignment in imbuedBonfires.Values)
        {
            if (alignment == AlignmentType.TreeEssence)
            {
                essenceCount++;
            }
            else if (alignment == AlignmentType.CreatureBlood)
            {
                bloodCount++;
            }
        }
    }

    private void ConfigureSceneBosses(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        EnsureBossHudForScene(scene);

        if (scene.name == "biome3_fight")
        {
            ConfigureFinalBossScene(scene);
        }
    }

    private void EnsureBossHudForScene(Scene scene)
    {
        if (!SceneNeedsBossHud(scene.name) || bossHudPrefab == null)
        {
            return;
        }

        BossUIManager existingBossUi = FindSceneObjectOfType<BossUIManager>(scene);
        if (existingBossUi != null || BossUIManager.Instance != null)
        {
            return;
        }

        GameObject bossHudInstance = Instantiate(bossHudPrefab);
        bossHudInstance.name = bossHudPrefab.name;
        SceneManager.MoveGameObjectToScene(bossHudInstance, scene);
    }

    private static bool SceneNeedsBossHud(string sceneName)
    {
        return sceneName == "biome2_subarea1" || sceneName == "biome3_fight";
    }

    private void ConfigureFinalBossScene(Scene scene)
    {
        OdinBoss odin = FindSceneObjectOfType<OdinBoss>(scene);
        HeimdallBoss heimdall = FindSceneObjectOfType<HeimdallBoss>(scene);

        if (odin == null || heimdall == null)
        {
            Debug.LogWarning($"[GameManager] Could not configure final boss in '{scene.name}' because Odin or Heimdall is missing.");
            return;
        }

        FinalBossType selectedBoss = DetermineFinalBoss();
        bool spawnOdin = selectedBoss == FinalBossType.Odin;

        odin.gameObject.SetActive(spawnOdin);
        heimdall.gameObject.SetActive(!spawnOdin);
    }

    private static T FindSceneObjectOfType<T>(Scene scene) where T : Component
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < objects.Length; i++)
        {
            T candidate = objects[i];
            if (candidate == null)
            {
                continue;
            }

            GameObject candidateObject = candidate.gameObject;
            if (candidateObject.scene == scene)
            {
                return candidate;
            }
        }

        return null;
    }
}
