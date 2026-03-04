using System;
using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [SerializeField] public Item itemData;
    public static event Action<Item> PickUpItem;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            PickUpItem?.Invoke(itemData);
            gameObject.SetActive(false);  
            Debug.Log("Player picked up item");
        }
    }

}
