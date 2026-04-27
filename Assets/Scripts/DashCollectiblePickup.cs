using UnityEngine;

public class DashCollectiblePickup : MonoBehaviour
{
    [SerializeField] private string uiCanvasName = "DashCollectedUICanvas";
    [SerializeField] private GameObject uiCanvasPrefab;

    private bool collected;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryCollect(collision.gameObject);
    }

    private void TryCollect(GameObject other)
    {
        if (collected || !other.CompareTag("Player")) return;

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
