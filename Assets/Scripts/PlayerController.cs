using UnityEngine;
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAttack))]

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
    private float xAxis, yAxis;

    [SerializeField] public float timeScale;

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

        playerMovement.defaultGravity = rb.gravityScale;

    }

    void Update()
    {
        GetInputs();
        playerMovement.Flip(xAxis);
        playerMovement.MaxFall(rb);
        playerMovement.Move(rb, xAxis, anim);
        playerMovement.Jump(rb, ref playerStates.isJumping, anim);
        
        playerAttack.Attack(playerStates.isAttacking, anim, yAxis, playerMovement);

        Time.timeScale = timeScale;
    }
    
    private void GetInputs()
    {
        xAxis = Input.GetAxisRaw("Horizontal");
        yAxis = Input.GetAxisRaw("Vertical");
        playerStates.isAttacking = Input.GetMouseButtonDown(0);
    }
}