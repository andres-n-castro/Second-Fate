using UnityEngine;

public class PlayerFootsteps : MonoBehaviour
{
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioSource jumpSource;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private float stepInterval = 0.25f;

    private float stepTimer;
    private bool wasMovingLastFrame;
    private bool wasGroundedLastFrame;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (rb != null && Mathf.Abs(rb.linearVelocity.y) > 0.05f)
        {
            isGrounded = false;
        }

        if (Input.GetKeyDown(KeyCode.Space) && jumpSound != null && jumpSource != null)
        {
            jumpSource.pitch = Random.Range(0.95f, 1.05f);
            jumpSource.PlayOneShot(jumpSound);
        }

        HandleFootsteps(isGrounded);

        wasGroundedLastFrame = isGrounded;
    }

    private void HandleFootsteps(bool isGrounded)
    {
        bool isMoving = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f;
        bool shouldPlayFootsteps = isGrounded && isMoving;

        if (shouldPlayFootsteps)
        {
            if (!wasMovingLastFrame || !wasGroundedLastFrame)
            {
                PlayFootstep();
                stepTimer = stepInterval;
            }
            else
            {
                stepTimer -= Time.deltaTime;

                if (stepTimer <= 0f)
                {
                    PlayFootstep();
                    stepTimer = stepInterval;
                }
            }
        }
        else
        {
            stepTimer = 0f;

            if (footstepSource != null && footstepSource.isPlaying)
            {
                footstepSource.Stop();
            }
        }

        wasMovingLastFrame = shouldPlayFootsteps;
    }

    private void PlayFootstep()
    {
        if (footstepSource == null || footstepSounds == null || footstepSounds.Length == 0) return;

        int index = Random.Range(0, footstepSounds.Length);
        footstepSource.clip = footstepSounds[index];
        footstepSource.pitch = Random.Range(0.88f, 1.12f);
        footstepSource.volume = Random.Range(0.85f, 1f);
        footstepSource.Play();
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}