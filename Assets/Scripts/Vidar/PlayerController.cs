using UnityEngine;
using System;
using System.Collections;


[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAttack))]

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip swordSwingSound;

    [Header("Combat Settings")]
    public float pogoBounceForce = 15f;
    public float deathAnimationLength = 1.5f;

    // Events for AI perception to track player actions
    public static event Action OnPlayerDashed;
    public static event Action OnPlayerAttacked;
    public static event Action OnPlayerJumped;
    public GameObject playerHud;
    private Rigidbody2D rb;
    private Animator anim;
    private PlayerMovement playerMovement;
    private PlayerAttack playerAttack;
    private PlayerStats playerStats;
    private PlayerStates playerStates;
    public SpriteRenderer spriteRenderer;
    private bool isHitStopping = false;
    private bool isExternallyFrozen = false;
    private float xAxis, yAxis;

    [SerializeField] public float timeScale = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        anim = GetComponent<Animator>();
        playerAttack = GetComponent<PlayerAttack>();
        playerStats = GetComponent<PlayerStats>();
        playerStates = GetComponent<PlayerStates>();
        spriteRenderer = GetComponent<SpriteRenderer>();


        playerMovement.defaultGravity = rb.gravityScale;

        if (playerStats != null && playerStats.playerHealthComponent != null)
        {
            playerStats.playerHealthComponent.OnDeath += HandlePlayerDeath;
        }
    }

    void OnDisable()
    {
        if (playerStats != null && playerStats.playerHealthComponent != null)
        {
            playerStats.playerHealthComponent.OnDeath -= HandlePlayerDeath;
        }
    }

    void Update()
    {
        UpdateKnockback();
        playerMovement.TickTimers();
        if (playerStates != null && playerStates.isDead)
        {
            return;
        }

        playerMovement.Flip(xAxis);
        playerMovement.MaxFall(rb);

        if (!playerStates.isKnockbacked)
        {
            GetInputs();
            playerMovement.Move(rb, xAxis, anim);
            playerMovement.Jump(rb, ref playerStates.isJumping, anim);
        }

        playerAttack.Attack(playerStates.isAttacking, anim, yAxis, playerMovement);

        if (GameManager.Instance == null ||
            GameManager.Instance.currentState == GameManager.GameState.Exploration ||
            GameManager.Instance.currentState == GameManager.GameState.BossFight ||
            GameManager.Instance.currentState == GameManager.GameState.Respawning)
        {
            Time.timeScale = isExternallyFrozen ? 0f : timeScale;
        }
    }

    public void KnockBack(Vector2 force, float timer)
    {
        playerStates.isKnockbacked = true;
        playerStates.knockbackTimer = timer;

        rb.linearVelocity = force;
    }

    private void UpdateKnockback()
    {
        if (!playerStates.isKnockbacked) return;
        playerStates.knockbackTimer -= Time.deltaTime;
        if (playerStates.knockbackTimer <= 0f)
        {
            playerStates.isKnockbacked = false;
        }
    }

    private void GetInputs()
    {
        bool canControlCharacter = GameManager.Instance == null ||
            ((GameManager.Instance.currentState == GameManager.GameState.Exploration ||
            GameManager.Instance.currentState == GameManager.GameState.BossFight) && !isExternallyFrozen);

        if (canControlCharacter)
        {
            xAxis = Input.GetAxisRaw("Horizontal");
            yAxis = Input.GetAxisRaw("Vertical");
            playerStates.isAttacking = Input.GetButtonDown("Player Attack");
        }
        else
        {
            xAxis = 0f;
            yAxis = 0f;
            playerStates.isAttacking = false;
        }

        if (playerStates.isAttacking)
        {
            OnPlayerAttacked?.Invoke();
        }

        bool dashPressed = Input.GetKeyDown(KeyCode.LeftShift) ||
            Input.GetButtonDown("Player Dash") ||
            Input.GetKeyDown(KeyCode.JoystickButton2);
        if (canControlCharacter && dashPressed && PlayerManager.Instance != null && PlayerManager.Instance.playerMovement != null)
        {
            PlayerManager.Instance.playerMovement.AttemptDash();
        }

        if (GameManager.Instance != null && Input.GetButtonDown("Open Inventory"))
        {
            if (GameManager.Instance.currentState == GameManager.GameState.Exploration)
            {
                GameManager.Instance.ChangeState(GameManager.GameState.InventoryMenu);
            }
            else if (GameManager.Instance.currentState == GameManager.GameState.InventoryMenu)
            {
                GameManager.Instance.RestorePreviousState();
            }
        }

        bool pausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton9);
        if (GameManager.Instance != null && pausePressed)
        {
            if (GameManager.Instance.currentState == GameManager.GameState.Exploration ||
                GameManager.Instance.currentState == GameManager.GameState.BossFight)
            {
                GameManager.Instance.ChangeState(GameManager.GameState.Paused);
            }
            else if (GameManager.Instance.currentState == GameManager.GameState.Paused)
            {
                GameManager.Instance.RestorePreviousState();
            }
        }
    }

    public void NotifyDashTriggered()
    {
        OnPlayerDashed?.Invoke();
    }

    public void NotifyJumpTriggered()
    {
        if (playerAttack != null)
        {
            playerAttack.DeactivateAllHitboxes();
        }

        OnPlayerJumped?.Invoke();
    }

    private void HandlePlayerDeath()
    {
        if (playerStates != null && playerStates.isDead)
        {
            return;
        }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (playerStates != null)
        {
            playerStates.isDead = true;
        }

        if (anim != null)
        {
            anim.SetTrigger("Death");
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        yield return new WaitForSeconds(deathAnimationLength);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerDeathMenu();
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        this.enabled = false;
    }

    public void ExecutePogoBounce()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, pogoBounceForce);

            if (playerStates != null)
            {
                playerStates.isJumping = true;
            }

            if (anim != null)
            {
                anim.SetTrigger("JumpTrigger");
            }

            StartCoroutine(PogoInvulnerabilityRoutine());

            Debug.Log("Pogo Bounce Executed! Velocity set to: " + pogoBounceForce);
        }
    }

    private IEnumerator PogoInvulnerabilityRoutine()
    {
        if (playerStats != null && playerStats.playerHealthComponent != null)
        {
            playerStats.playerHealthComponent.isInvulnerable = true;
            yield return new WaitForSeconds(0.15f);
            playerStats.playerHealthComponent.isInvulnerable = false;
        }
    }

    // Call this from anywhere to freeze the game
    public void TriggerHitStop(float duration)
    {
        if (isExternallyFrozen) return;
        if (isHitStopping) return; // Prevents overlapping hitstops from breaking the timer
        StartCoroutine(HitStopRoutine(duration));
    }

    public IEnumerator HitStopRoutine(float duration)
    {
        isHitStopping = true;
        timeScale = 0f; // Freeze the game

        yield return new WaitForSecondsRealtime(duration); // Wait in real-world milliseconds

        if (!isExternallyFrozen)
        {
            timeScale = 1f; // Unfreeze
        }
        isHitStopping = false;
    }

    public void SetExternalFreeze(bool frozen)
    {
        isExternallyFrozen = frozen;

        if (frozen)
        {
            timeScale = 0f;
            Time.timeScale = 0f;
        }
        else
        {
            timeScale = 1f;
            Time.timeScale = 1f;
        }
    }

    public void PlaySwingSound()
    {
        if (sfxSource.isPlaying) return;

        if (sfxSource != null && swordSwingSound != null)
        {
            sfxSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            sfxSource.PlayOneShot(swordSwingSound);
        }
    }
}