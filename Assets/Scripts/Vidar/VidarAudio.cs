using UnityEngine;

/// <summary>
/// Audio for the player (Vidar). Handles four separate sounds:
///   1. Damage SFX — random clip from damageSounds[] with random pitch.
///      Fires on Health.OnDamageTaken.
///   2. Death SFX  — single clip (or random pick from deathSounds[]).
///      Fires once on Health.OnDeath.
///   3. Jump SFX   — random clip from jumpSounds[] with slight pitch wobble.
///      Fires on PlayerController.OnPlayerJumped (covers both ground and double jumps).
///   4. Dash SFX   — random clip from dashSounds[] with slight pitch wobble.
///      Fires on PlayerController.OnPlayerDashed.
///
/// Place this on the player GameObject. It will auto-find the Health component
/// via PlayerStats. All clips use PlayOneShot so they overlap cleanly with
/// other player audio (footsteps, sword swings, etc.).
/// </summary>
public class VidarAudio : MonoBehaviour
{
    [Header("Audio Source")]
    [Tooltip("AudioSource used for all player audio events. Untick Play On Awake.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Damage SFX")]
    [SerializeField] private AudioClip[] damageSounds;
    [SerializeField] private float damageVolume = 1f;
    [SerializeField] private float damageMinPitch = 0.9f;
    [SerializeField] private float damageMaxPitch = 1.1f;

    [Header("Death SFX")]
    [SerializeField] private AudioClip[] deathSounds;
    [SerializeField] private float deathVolume = 1f;

    [Header("Jump SFX")]
    [SerializeField] private AudioClip[] jumpSounds;
    [SerializeField] private float jumpVolume = 1f;
    [SerializeField] private float jumpMinPitch = 0.95f;
    [SerializeField] private float jumpMaxPitch = 1.05f;

    [Header("Dash SFX")]
    [SerializeField] private AudioClip[] dashSounds;
    [SerializeField] private float dashVolume = 1f;
    [SerializeField] private float dashMinPitch = 0.95f;
    [SerializeField] private float dashMaxPitch = 1.05f;

    private Health playerHealth;
    private bool hasDied;
    private bool damageHooked;

    private void OnEnable()
    {
        PlayerController.OnPlayerJumped += HandleJump;
        PlayerController.OnPlayerDashed += HandleDash;

        TryHookHealth();
    }

    private void OnDisable()
    {
        PlayerController.OnPlayerJumped -= HandleJump;
        PlayerController.OnPlayerDashed -= HandleDash;

        if (playerHealth != null)
        {
            playerHealth.OnDamageTaken -= HandleDamageTaken;
            playerHealth.OnDeath -= HandleDeath;
        }
        damageHooked = false;
    }

    private void Start()
    {
        // PlayerStats may finish wiring its Health reference in its own Start, so retry here.
        TryHookHealth();
    }

    private void TryHookHealth()
    {
        if (damageHooked) return;

        var stats = GetComponent<PlayerStats>();
        if (stats != null && stats.playerHealthComponent != null)
        {
            playerHealth = stats.playerHealthComponent;
        }
        else
        {
            playerHealth = GetComponent<Health>();
        }

        if (playerHealth != null)
        {
            playerHealth.OnDamageTaken += HandleDamageTaken;
            playerHealth.OnDeath += HandleDeath;
            damageHooked = true;
        }
    }

    private void HandleDamageTaken(int damage, Vector2 knockback)
    {
        if (hasDied) return;
        PlayRandom(damageSounds, damageVolume, damageMinPitch, damageMaxPitch);
    }

    private void HandleDeath()
    {
        if (hasDied) return;
        hasDied = true;
        PlayRandom(deathSounds, deathVolume, 1f, 1f);
    }

    private void HandleJump()
    {
        if (hasDied) return;
        PlayRandom(jumpSounds, jumpVolume, jumpMinPitch, jumpMaxPitch);
    }

    private void HandleDash()
    {
        if (hasDied) return;
        PlayRandom(dashSounds, dashVolume, dashMinPitch, dashMaxPitch);
    }

    private void PlayRandom(AudioClip[] clips, float volume, float minPitch, float maxPitch)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(clip, volume);
    }
}
