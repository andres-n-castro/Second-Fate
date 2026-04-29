using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTeleporter : MonoBehaviour
{
    [Header("Teleporter Identity")]
    [SerializeField] private string teleporterID;

    [Header("Destination")]
    [SerializeField] private string sceneToLoad;
    [SerializeField] private string destinationTeleporterID;

    [Header("Spawn Point")]
    [Tooltip("Where arriving players appear when teleporting TO this portal. If empty, uses this object's position.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Interaction")]
    [SerializeField] private GameObject interactionPrompt;

    private bool _isPlayerInZone = false;
    private const KeyCode PortalInteractButton = KeyCode.JoystickButton3;

    /// <summary>
    /// Persists across scene loads because it is static on the class type.
    /// Stores the teleporterID of the destination portal the player should spawn at.
    /// </summary>
    private static string pendingArrivalTeleporterID;

    private void Start()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }

        // If this teleporter is the arrival destination, place the player here.
        if (!string.IsNullOrEmpty(pendingArrivalTeleporterID)
            && teleporterID == pendingArrivalTeleporterID)
        {
            pendingArrivalTeleporterID = null;
            StartCoroutine(PlacePlayerAtSpawnPoint());
        }
    }

    private IEnumerator PlacePlayerAtSpawnPoint()
    {
        // Wait for PlayerManager to be available (may take a few frames after scene load).
        for (int i = 0; i < 10 && PlayerManager.Instance == null; i++)
        {
            yield return null;
        }

        if (PlayerManager.Instance == null)
        {
            Debug.LogWarning($"[SceneTeleporter] Could not place player at '{teleporterID}' because PlayerManager is null.");
            yield break;
        }

        Transform target = spawnPoint != null ? spawnPoint : transform;
        PlayerManager.Instance.transform.position = target.position;

        Rigidbody2D rb = PlayerManager.Instance.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentRespawnPoint = target.position;
        }

        Debug.Log($"[SceneTeleporter] Player placed at teleporter '{teleporterID}' position {target.position}.");
    }

    void Update()
    {
        bool interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(PortalInteractButton);

        if (_isPlayerInZone
            && interactPressed
            && GameManager.Instance != null
            && GameManager.Instance.currentState == GameManager.GameState.Exploration)
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveCurrentSlotForScene(sceneToLoad);
            }

            pendingArrivalTeleporterID = destinationTeleporterID;
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInZone = true;
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInZone = false;
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
    }
}
