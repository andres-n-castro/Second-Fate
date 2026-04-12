using UnityEngine;

public class FWSAudio : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] idleSounds;

    [Header("Detection")]
    [SerializeField] private Transform player;
    [SerializeField] private float detectionRange = 6f;

    [Header("Timing")]
    [SerializeField] private float minDelay = 2f;
    [SerializeField] private float maxDelay = 5f;

    private float timer;
    private bool playerInRange;

    void Start()
    {
        ResetTimer();
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        playerInRange = distance <= detectionRange;

        if (!playerInRange)
        {
            timer = 0f;
            return;
        }

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            PlaySound();
            ResetTimer();
        }
    }

    private void PlaySound()
    {
        if (audioSource == null || idleSounds.Length == 0) return;

        int index = Random.Range(0, idleSounds.Length);
        audioSource.pitch = Random.Range(0.6f, 0.9f);
        audioSource.PlayOneShot(idleSounds[index]);
    }

    private void ResetTimer()
    {
        timer = Random.Range(minDelay, maxDelay);
    }
}