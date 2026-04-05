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

    // Events for AI perception to track player actions
    public static event Action OnPlayerDashed;
    public static event Action OnPlayerAttacked;
    public GameObject playerHud;
    private Rigidbody2D rb;
    private Animator anim;
    private PlayerMovement playerMovement;
    private PlayerAttack playerAttack;
    private PlayerStats playerStats;
    private PlayerStates playerStates;
    public SpriteRenderer spriteRenderer;
    private bool isHitStopping = false;
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

    }

    void Update()
    {
        UpdateKnockback();
        playerMovement.TickTimers();
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
            Time.timeScale = timeScale;
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
            GameManager.Instance.currentState == GameManager.GameState.Exploration ||
            GameManager.Instance.currentState == GameManager.GameState.BossFight;

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

        if (GameManager.Instance != null && Input.GetKeyDown(KeyCode.Escape))
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

    // Call this from anywhere to freeze the game
    public void TriggerHitStop(float duration)
    {
        if (isHitStopping) return; // Prevents overlapping hitstops from breaking the timer
        StartCoroutine(HitStopRoutine(duration));
    }

    public IEnumerator HitStopRoutine(float duration)
    {
        isHitStopping = true;
        timeScale = 0f; // Freeze the game

        yield return new WaitForSecondsRealtime(duration); // Wait in real-world milliseconds

        timeScale = 1f; // Unfreeze
        isHitStopping = false;
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