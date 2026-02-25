using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private float attackCooldown = 0.1f; 
    private float timeSinceAttack; 
    [SerializeField] Transform sideAttackTransform, upAttackTransform, downAttackTransform;
    [SerializeField] Vector2 sideAttackArea, upAttackArea, downAttackArea;

    [SerializeField] LayerMask attackLayer;
    
    public void Attack(bool isAttacking, Animator anim, float yAxis, PlayerMovement playerMovement)
    {
        timeSinceAttack += Time.deltaTime;
        if (isAttacking && timeSinceAttack >= attackCooldown)
        {
            timeSinceAttack = 0; 

            if(yAxis == 0 || yAxis < 0 && playerMovement.Grounded())
            {
                anim.SetTrigger("Attacking");
                Hit(sideAttackTransform, sideAttackArea);
            }
            else if (yAxis > 0)
            {
                anim.SetTrigger("UpAttack");
                Hit(upAttackTransform, upAttackArea);
            }
            else if(yAxis < 0 && !playerMovement.Grounded())
            {
                anim.SetTrigger("DownAttack");
                Hit(downAttackTransform, downAttackArea);
            }
        }

    }

    private void Hit(Transform attackTransform, Vector2 attackArea)
    {
        Collider2D[] objectsToHit = Physics2D.OverlapBoxAll(attackTransform.position, attackArea, 0, attackLayer);

        if(objectsToHit.Length > 0)
        {
            Debug.Log("Hit");
        }
        
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(sideAttackTransform.position, sideAttackArea);
        Gizmos.DrawWireCube(upAttackTransform.position, upAttackArea);   
        Gizmos.DrawWireCube(downAttackTransform.position, downAttackArea);          
    }
}
