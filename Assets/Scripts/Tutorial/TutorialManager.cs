using System.Collections.Generic;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    private HashSet<string> collectedKeys = new HashSet<string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        Debug.LogWarning($"[TutorialManager] Duplicate TutorialManager found on '{name}'. Removing the component only.");
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void AddKey(string id)
    {
        collectedKeys.Add(id);
    }

    public bool HasKey(string id)
    {
        return collectedKeys.Contains(id);
    }
}
