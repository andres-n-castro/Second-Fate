using System;
using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [SerializeField] public Item itemData;
    public static event Action<Item> PickUpItem;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PickUpItem?.Invoke(itemData);
            gameObject.SetActive(false);  
            Debug.Log("Player picked up item");
        }
    }

}
