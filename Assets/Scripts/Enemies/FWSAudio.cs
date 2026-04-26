using UnityEngine;

/// <summary>
/// Audio for the Fallen Warrior Spirit (FWS) enemy. Handles three things:
///   1. Periodic idle "growls" while the player is within detection range
///      (random clip, random pitch, random volume). The next growl will
///      not start until the previous clip has finished.
///   2. A hurt SFX whenever the FWS's Health component takes damage.
///   3. A death SFX whenever the Health component reaches zero.
///
/// Place this component on the same GameObject as the FWS / Health.
/// Drag in an AudioSource (the same one used for everything is fine — the
/// hurt and death sounds use PlayOneShot so they won't cut off the growls).
/// </summary>
[RequireComponent(typeof(Health))]
public class FWSAudio : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Idle Growls")]
    [SerializeField] private AudioClip[] idleSounds;
    [SerializeField] private float growlMinPitch = 0.6f;
    [SerializeField] private float growlMaxPitch = 0.9f;
    [SerializeField] private float growlMinVolume = 0.4f;
    [SerializeField] private float growlMaxVolume = 1.0f;

    [Header("Damage SFX")]
    [Tooltip("Played whenever this FWS takes damage. One is picked at random.")]
    [SerializeField] private AudioClip[] damageSounds;
    [SerializeField] private float damageVolume = 1f;
    [SerializeField] private float damageMinPitch = 0.95f;
    [SerializeField] private float damageMaxPitch = 1.05f;

    [Header("Death SFX")]
    [Tooltip("Played once when this FWS dies. One is picked at random.")]
    [SerializeField] private AudioClip[] deathSounds;
    [SerializeField] private float deathVolume = 1f;

    [Header("Detection")]
    [SerializeField] private Transform player;
    [SerializeField] private float detectionRange = 6f;

    [Header("Timing")]
    [SerializeField] private float minDelay = 2f;
    [SerializeField] private float maxDelay = 5f;

    private Health health;
    private float timer;
    private bool playerInRange;
    private bool hasDied;
    private float growlClipEndTime;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (health != null)
        {
            health.OnDamageTaken += HandleDamageTaken;
            health.OnDeath += HandleDeath;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamageTaken -= HandleDamageTaken;
            health.OnDeath -= HandleDeath;
        }
    }

    private void Start()
    {
        if (player == null && PlayerController.Instance != null)
        {
            player = PlayerController.Instance.transform;
        }

        ResetTimer();
    }

    private void Update()
    {
        if (hasDied) return;
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        playerInRange = distance <= detectionRange;

        if (!playerInRange)
        {
            timer = 0f;
            return;
        }

        // Wait for the previous growl clip to finish before counting down the next delay.
        if (Time.time < growlClipEndTime) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            PlayGrowl();
            ResetTimer();
        }
    }

    private void PlayGrowl()
    {
        if (audioSource == null || idleSounds == null || idleSounds.Length == 0) return;

        AudioClip clip = idleSounds[Random.Range(0, idleSounds.Length)];
        if (clip == null) return;

        float pitch = Random.Range(growlMinPitch, growlMaxPitch);
        audioSource.pitch = pitch;
        float volume = Random.Range(growlMinVolume, growlMaxVolume);
        audioSource.PlayOneShot(clip, volume);

        // Block the next growl from starting until this clip finishes (account for pitch).
        growlClipEndTime = Time.time + clip.length / Mathf.Max(0.01f, pitch);
    }

    private void HandleDamageTaken(int damage, Vector2 knockback)
    {
        if (hasDied) return;
        if (audioSource == null || damageSounds == null || damageSounds.Length == 0) return;

        AudioClip clip = damageSounds[Random.Range(0, damageSounds.Length)];
        if (clip == null) return;

        audioSource.pitch = Random.Range(damageMinPitch, damageMaxPitch);
        audioSource.PlayOneShot(clip, damageVolume);
    }

    private void HandleDeath()
    {
        if (hasDied) return;
        hasDied = true;

        if (audioSource == null || deathSounds == null || deathSounds.Length == 0) return;

        AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
        if (clip == null) return;

        audioSource.pitch = 1f;
        audioSource.PlayOneShot(clip, deathVolume);
    }

    private void ResetTimer()
    {
        timer = Random.Range(minDelay, maxDelay);
    }
}
