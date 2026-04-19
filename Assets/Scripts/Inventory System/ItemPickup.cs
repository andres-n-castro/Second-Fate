using System;
using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [SerializeField] public Item itemData;
    public static event Action<Item> PickUpItem;

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckPickup(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckPickup(collision.gameObject);
    }

    private void CheckPickup(GameObject otherObject)
    {
        if (otherObject.CompareTag("Player"))
        {
            PickUpItem?.Invoke(itemData);
            gameObject.SetActive(false);  
            Debug.Log("Player picked up item: " + itemData.itemName);
        }
    }
}
