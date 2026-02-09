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

    IEnumerator HandleSpikeHit()
    {
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(fadeDelay);

        if (currentCheckpoint != null)
        {
            transform.position = currentCheckpoint.position;
        }

        rb.simulated = true;
    }
}
