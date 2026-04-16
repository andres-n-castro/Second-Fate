using UnityEngine;
using System.Collections;

public class PlatformDrop : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float dropDelay = 0.3f;
    [SerializeField] private LayerMask platformLayer;

    private PlatformEffector2D _currentEffector;

    void Update()
    {
        // Check for 'S' to drop through platform
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (CheckForPlatform())
            {
                StartCoroutine(DropRoutine());
            }
        }
    }

    private bool CheckForPlatform()
    {
        // Shoot a tiny ray downwards from the player's feet
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.5f, platformLayer);

        if (hit.collider != null)
        {
            // Try to find the effector on the object we hit or its parent
            _currentEffector = hit.collider.gameObject.GetComponent<PlatformEffector2D>();
            return _currentEffector != null;
        }
        return false;
    }

    private IEnumerator DropRoutine()
    {
        // Flip the effector
        _currentEffector.rotationalOffset = 180f;
        yield return new WaitForSeconds(dropDelay);
        _currentEffector.rotationalOffset = 0f;
        _currentEffector = null;
    }
}