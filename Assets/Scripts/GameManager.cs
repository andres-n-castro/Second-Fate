using UnityEngine;
using System;
using UnityEngine.InputSystem.XR.Haptics;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance;
    public GameState currentState {get; private set;}
    public static event Action explorationEvent;
    public static event Action treeRootRestEvent;
    public static event Action merchantInteractionEvent;
    public static event Action playerDeathEvent;
    public static event Action bossEncounterEvent;
    public static event Action playerPauseEvent;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void UpdateGameState(GameState newState)
    {

        currentState = newState;

        switch (newState)
        {
            case GameState.exploration:
                Time.timeScale = 1f;
                explorationEvent?.Invoke();
                break;
            case GameState.treeRootRest:
                Time.timeScale = 1f;
                treeRootRestEvent?.Invoke();
                break;
            case GameState.playerDeath:
                Time.timeScale = 1f;
                playerDeathEvent?.Invoke();
                break;
            case GameState.merchantInteraction:
                Time.timeScale = 0f;
                merchantInteractionEvent?.Invoke();
                break;
            case GameState.bossEncounter:
                Time.timeScale = 1f;
                bossEncounterEvent?.Invoke();
                break;
            case GameState.playerPause:
                Time.timeScale = 0f;
                playerPauseEvent?.Invoke();
                break;
        }
    }

    public enum GameState
    {
        exploration,
        treeRootRest,
        merchantInteraction,
        playerDeath,
        bossEncounter,
        playerPause
    }


    
}
