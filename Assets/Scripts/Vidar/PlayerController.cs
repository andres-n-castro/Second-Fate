using UnityEngine;
using System;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAttack))]

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    // Event for AI perception to track player dashes
    public static event Action OnPlayerDashed;
    private Rigidbody2D rb;
    private Animator anim;
    private PlayerMovement playerMovement;
    private PlayerAttack playerAttack;
    private PlayerStats playerStats;
    private PlayerStates playerStates;
    public SpriteRenderer spriteRenderer;
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
        GetInputs();
        playerMovement.Flip(xAxis);
        playerMovement.MaxFall(rb);

        if (!playerStates.isKnockbacked)
        {
            playerMovement.Move(rb, xAxis, anim);
            playerMovement.Jump(rb, ref playerStates.isJumping, anim);
        }

        playerAttack.Attack(playerStates.isAttacking, anim, yAxis, playerMovement);

        Time.timeScale = timeScale;
    }

    public void KnockBack(Vector2 force, float timer)
    {
        playerStates.isKnockbacked = true;
        playerStates.knockbackTimer = timer;

        rb.linearVelocity = Vector2.zero;

        rb.AddForce(force, ForceMode2D.Impulse);
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
        xAxis = Input.GetAxisRaw("Horizontal");
        yAxis = Input.GetAxisRaw("Vertical");
        playerStates.isAttacking = Input.GetMouseButtonDown(0);
    }
}