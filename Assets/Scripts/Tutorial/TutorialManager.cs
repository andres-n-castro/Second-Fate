using System.Collections.Generic;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    private static readonly HashSet<string> collectedKeys = new HashSet<string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
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

    public static TutorialManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject managerObject = new GameObject("TutorialManager");
        return managerObject.AddComponent<TutorialManager>();
    }

    public void AddKey(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        collectedKeys.Add(id);
    }

    public bool HasKey(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && collectedKeys.Contains(id);
    }

    public List<string> GetCollectedKeys()
    {
        return new List<string>(collectedKeys);
    }

    public void SetCollectedKeys(IEnumerable<string> keyIDs)
    {
        collectedKeys.Clear();

        if (keyIDs == null)
        {
            return;
        }

        foreach (string keyID in keyIDs)
        {
            AddKey(keyID);
        }
    }
}
