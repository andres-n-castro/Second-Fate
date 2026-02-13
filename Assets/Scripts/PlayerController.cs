using System.Runtime.CompilerServices;
using UnityEngine;
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAttack))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerStates))]

public class PlayerController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private PlayerController Instance;
    private Rigidbody2D rb;
    private Animator anim;
    private PlayerMovement playerMovement;
    private PlayerAttack playerAttack;
    private PlayerStats playerStats;
    private PlayerStates playerStates;
    private float xAxis;


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
        playerMovement = GetComponent<PlayerMovement>();
        playerAttack = GetComponent<PlayerAttack>();
        playerStats = GetComponent<PlayerStats>();
        playerStates = GetComponent<PlayerStates>();

        playerMovement.defaultGravity = rb.gravityScale;

    }

    void Update()
    {
        GetInputs();
        playerMovement.Flip(xAxis);
        playerMovement.MaxFall(rb);
        playerMovement.Move(rb, xAxis, anim);
        playerMovement.Jump(rb, playerStates.isJumping, anim);
        
        playerAttack.Attack(playerStates.isAttacking, anim);
    }
    
    private void GetInputs()
    {
        xAxis = Input.GetAxisRaw("Horizontal");
        playerStates.isAttacking = Input.GetMouseButtonDown(0);
    }
}