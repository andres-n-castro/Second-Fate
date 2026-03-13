using UnityEngine;
using System.Collections;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn Settings")]
    public Transform currentCheckpoint;
    public float fadeDelay = 0.2f;

    private Rigidbody2D rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Hazard"))
        {
            StartCoroutine(HandleSpikeHit());
        }

        if (other.CompareTag("Checkpoint"))
        {
            currentCheckpoint = other.transform;
            Debug.Log("Progress Saved at: " + other.gameObject.name);
        }
    }

    public void RespawnPlayer()
    {
        StartCoroutine(HandleSpikeHit());
    }

    public IEnumerator HandleSpikeHit()
    {
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;

        // CLEAR ALL STATES so the player isn't stuck "dead" or "knockbacked"
        PlayerStates states = GetComponent<PlayerStates>();
        states.isKnockbacked = false;
        states.isDead = true; // Stay "dead" during the fade/teleport

        yield return new WaitForSeconds(fadeDelay);

        if (currentCheckpoint != null)
        {
            transform.position = currentCheckpoint.position;
            rb.position = currentCheckpoint.position;
        }

        // Wait for physics to catch up
        yield return new WaitForFixedUpdate();

        rb.simulated = true;
        states.isDead = false; // NOW they can move again
    }
}
