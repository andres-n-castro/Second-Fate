using UnityEngine;

public class DashCollectiblePickup : MonoBehaviour
{
    [SerializeField] private string uiCanvasName = "DashCollectedUICanvas";
    [SerializeField] private GameObject uiCanvasPrefab;
    [SerializeField] private float collectDelay = 1.5f;

    private bool collected;
    private bool collectEnabled;

    private void Start()
    {
        // Disable collection briefly so the collectible can visually pop out
        // before the player can pick it up (prevents UI overlap with boss death popup).
        collectEnabled = false;
        StartCoroutine(EnableCollectionAfterDelay());
    }

    private System.Collections.IEnumerator EnableCollectionAfterDelay()
    {
        yield return new WaitForSecondsRealtime(collectDelay);
        collectEnabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryCollect(collision.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other.gameObject);
    }

    private void TryCollect(GameObject other)
    {
        if (collected || !collectEnabled || !other.CompareTag("Player")) return;

        PlayerStats playerStats = PlayerManager.Instance != null ? PlayerManager.Instance.playerStats : null;
        if (playerStats == null) return;

        collected = true;
        playerStats.canDash = true;
        playerStats.hasDash = true;

        GameObject unlockCanvas = FindSceneObjectByName(uiCanvasName);
        if (unlockCanvas == null && uiCanvasPrefab != null)
        {
            unlockCanvas = Instantiate(uiCanvasPrefab);
        }

        if (unlockCanvas != null)
        {
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.SetExternalFreeze(true);
            }

            unlockCanvas.SetActive(true);
            Time.timeScale = 0f;
        }

        Destroy(gameObject);
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;

        foreach (GameObject sceneObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (sceneObject.name == objectName && sceneObject.scene.IsValid())
            {
                return sceneObject;
            }
        }

        return null;
    }
}
