using UnityEngine;
using System.Collections;

public class PlatformDrop : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float dropDelay = 0.3f;
    [SerializeField] private LayerMask platformLayer;

    private Collider2D currentPlatformCollider;
    private Collider2D[] playerColliders;

    private void Awake()
    {
        playerColliders = GetComponentsInChildren<Collider2D>();
    }

    void Update()
    {
        // Check for 'S' to drop through platform
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (CheckForPlatform())
            {
                StartCoroutine(DropRoutine(currentPlatformCollider));
            }
        }
    }

    private bool CheckForPlatform()
    {
        // Shoot a tiny ray downwards from the player's feet
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.5f, platformLayer);

        if (hit.collider != null)
        {
            currentPlatformCollider = hit.collider;
            return currentPlatformCollider.GetComponent<PlatformEffector2D>() != null
                || currentPlatformCollider.GetComponentInParent<PlatformEffector2D>() != null;
        }
        return false;
    }

    private IEnumerator DropRoutine(Collider2D platformCollider)
    {
        if (platformCollider == null)
        {
            yield break;
        }

        foreach (Collider2D playerCollider in playerColliders)
        {
            if (playerCollider != null)
            {
                Physics2D.IgnoreCollision(playerCollider, platformCollider, true);
            }
        }

        yield return new WaitForSeconds(dropDelay);

        foreach (Collider2D playerCollider in playerColliders)
        {
            if (playerCollider != null && platformCollider != null)
            {
                Physics2D.IgnoreCollision(playerCollider, platformCollider, false);
            }
        }

        currentPlatformCollider = null;
    }
}