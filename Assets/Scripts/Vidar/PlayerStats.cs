using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{

    [Header("Health System")]
    public int maxHealth = 4;
    public int currentHealth;

    [Header("Powers System")]
    public bool hasDoubleJump = false;
    public bool hasDash = false;

    [Header("Charms System")]
    public List<bool> Charms;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        
    }
}
