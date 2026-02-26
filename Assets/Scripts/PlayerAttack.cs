using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerAttack : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private float timeBetweenAttack; 
    private float timeSinceAttack; 
    
    public void Attack(bool isAttacking, Animator anim)
    {
        timeSinceAttack += Time.deltaTime;
        if (isAttacking && timeSinceAttack >= timeBetweenAttack)
        {
           timeSinceAttack = 0; 
           anim.SetTrigger("Attacking");
        }
    }
}
