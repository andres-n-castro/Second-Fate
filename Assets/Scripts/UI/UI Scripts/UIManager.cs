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

    void Start()
    {
         UIStateChanged?.Invoke(UIStates.playerUI);
    }

    void UpdateUIState(UIStates newState)
    {
        Debug.Log(newState);
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
        PlayerController.OnInputInventory -= UpdateUIState;
    }
}
