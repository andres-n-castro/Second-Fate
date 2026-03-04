using System;
using UnityEngine;

public class UIManager : MonoBehaviour
{

    public static UIManager Instance;
    public static UIStates uiManagerCurrentState;
    public static event Action<UIStates> UIStateChanged;

    void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }

        Instance = this;
    }

    void UpdateUIState(UIStates newState)
    {
        if(uiManagerCurrentState == newState) return;

        uiManagerCurrentState = newState;

        UIStateChanged?.Invoke(newState);
    }

    public enum UIStates
    {
        playerUI,
        inventoryUI,
        charmsUI,
        mainMenuUI,
        tutorialMenulUI,
        optionsMenuUI,
        merchantMenutUI,
        bonfireMenuUI,
        

    } 

    void OnEnable()
    {
        PlayerController.OnInputInventory += UpdateUIState;
    }

    void OnDisable()
    {
        
    }
}
