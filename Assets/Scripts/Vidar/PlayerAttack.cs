using UnityEngine;
using System.Collections.Generic;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private float attackCooldown = 0.1f; 
    private float timeSinceAttack; 
    [SerializeField] Transform sideAttackTransform, upAttackTransform, downAttackTransform;
    [SerializeField] Vector2 sideAttackArea, upAttackArea, downAttackArea;

    [SerializeField] LayerMask attackLayer;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private Vector2 knockbackForce = new Vector2(5f, 2f);

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
        List<IDamageable> alreadyHit = new List<IDamageable>();

        for (int i = 0; i < objectsToHit.Length; i++)
        {
            IDamageable target = objectsToHit[i].GetComponent<IDamageable>();
            if (target == null)
                target = objectsToHit[i].GetComponentInParent<IDamageable>();

            if (target == null || alreadyHit.Contains(target)) continue;

            alreadyHit.Add(target);

            Vector2 direction = (objectsToHit[i].transform.position - attackTransform.position).normalized;
            Vector2 kb = new Vector2(direction.x * knockbackForce.x, knockbackForce.y);
            target.TakeDamage(attackDamage, kb);
            Debug.Log($"Sword hit {objectsToHit[i].name}!\n Hitbox hit: knockback={kb} ");
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
