using System;
using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [SerializeField] public Item itemData;

    [Header("Pickup SFX")]
    [Tooltip("Optional sound played when this item is picked up. If multiple are provided, one is chosen at random.")]
    [SerializeField] private AudioClip[] pickupSounds;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 1f;
    [SerializeField] private float pickupMinPitch = 0.95f;
    [SerializeField] private float pickupMaxPitch = 1.05f;

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

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification("Item added to inventory");
            }

            PlayPickupSound();

            gameObject.SetActive(false);
            Debug.Log("Player picked up item: " + itemData.itemName);
        }
    }

    private void PlayPickupSound()
    {
        if (pickupSounds == null || pickupSounds.Length == 0) return;

        AudioClip clip = pickupSounds[UnityEngine.Random.Range(0, pickupSounds.Length)];
        if (clip == null) return;

        // Spawn a temporary AudioSource so the sound survives this GameObject being disabled.
        GameObject sfx = new GameObject($"PickupSFX_{clip.name}");
        sfx.transform.position = transform.position;
        AudioSource src = sfx.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = pickupVolume;
        src.pitch = UnityEngine.Random.Range(pickupMinPitch, pickupMaxPitch);
        src.spatialBlend = 0f; // 2D sound
        src.Play();
        Destroy(sfx, clip.length / Mathf.Max(0.01f, src.pitch) + 0.1f);
    }
}
