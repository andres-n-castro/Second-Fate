using UnityEngine;
using System.Collections;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn Settings")]
    [Tooltip("When true, dying will respawn the player at the last checkpoint touched instead of the last rested bonfire. Enable this only on the first scene (e.g. tutorial_hub) where the player can die before reaching a bonfire.")]
    public bool useCheckpointRespawn = false;
    public Transform currentCheckpoint;
    public float fadeDelay = 0.2f;

    [Header("Hazard Settings")]
    [Tooltip("Damage dealt per tick while standing in a Hazard.")]
    public int hazardDamagePerTick = 1;
    [Tooltip("Seconds between hazard damage ticks.")]
    public float hazardTickInterval = 1f;

    private Rigidbody2D rb;
    private Health health;
    private PlayerStats playerStats;

    private int hazardContactCount = 0;
    private Coroutine hazardRoutine;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerStats = GetComponent<PlayerStats>();

        if (playerStats != null && playerStats.playerHealthComponent != null)
        {
            health = playerStats.playerHealthComponent;
        }
        else
        {
            health = GetComponent<Health>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Hazard"))
        {
            hazardContactCount++;
            if (hazardRoutine == null)
            {
                hazardRoutine = StartCoroutine(HazardDamageLoop());
            }
        }

        if (other.CompareTag("Checkpoint"))
        {
            currentCheckpoint = other.transform;
            Debug.Log("Progress Saved at: " + other.gameObject.name);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Hazard"))
        {
            hazardContactCount = Mathf.Max(0, hazardContactCount - 1);
            if (hazardContactCount == 0 && hazardRoutine != null)
            {
                StopCoroutine(hazardRoutine);
                hazardRoutine = null;
            }
        }
    }

    private void OnDisable()
    {
        if (hazardRoutine != null)
        {
            StopCoroutine(hazardRoutine);
            hazardRoutine = null;
        }
        hazardContactCount = 0;
    }

    private IEnumerator HazardDamageLoop()
    {
        // Deal damage on contact, then repeat on interval while still inside the hazard.
        while (hazardContactCount > 0)
        {
            if (health != null && !health.IsDead)
            {
                health.TakeDamage(hazardDamagePerTick, Vector2.zero);
            }

            yield return new WaitForSeconds(hazardTickInterval);
        }

        hazardRoutine = null;
    }

    /// <summary>
    /// Teleports the player to the last touched checkpoint. Used by the respawn
    /// flow in the starting scene where the player may die before reaching a bonfire.
    /// Returns true if a checkpoint was available and used.
    /// </summary>
    public bool RespawnAtCheckpoint()
    {
        if (currentCheckpoint == null)
        {
            return false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        transform.position = currentCheckpoint.position;
        return true;
    }

    /// <summary>
    /// Legacy fade-out respawn used as a local fallback (no GameManager).
    /// </summary>
    public IEnumerator HandleSpikeHit()
    {
        if (rb != null)
        {
            rb.simulated = false;
            rb.linearVelocity = Vector2.zero;
        }

        yield return new WaitForSeconds(fadeDelay);

        RespawnAtCheckpoint();

        if (rb != null)
        {
            rb.simulated = true;
        }
    }
}
