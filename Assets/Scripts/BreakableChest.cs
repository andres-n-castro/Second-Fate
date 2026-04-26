using System.Collections;
using UnityEngine;

public class BreakableChest : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public int maxHealth = 5;
    private int currentHealth;

    [Header("Loot Settings")]
    public GameObject coinPrefab;
    public int coinsToSpawn = 10;
    public int currencyPerCoin = 5;

    [Header("Hit Feedback")]
    public float shakeDuration = 0.15f;
    public float shakeMagnitude = 0.1f;
    private bool isShaking = false;
    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;
    private bool isBroken = false;
    private Rigidbody2D rb;

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        originalPosition = transform.position;
    }

    private void OnEnable()
    {
        originalPosition = transform.position;
    }

    private void OnDisable()
    {
        if (!isBroken)
        {
            SetPosition(originalPosition);
        }
    }

    public void TakeDamage(int damage, Vector2 knockbackForce)
    {
        if (isBroken || currentHealth <= 0) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            BreakOpen();
        }
        else if (!isShaking)
        {
            shakeCoroutine = StartCoroutine(ShakeRoutine());
        }
    }

    private void BreakOpen()
    {
        if (isBroken) return;
        isBroken = true;

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        SetPosition(originalPosition);

        foreach (Collider2D col in GetComponents<Collider2D>())
        {
            col.enabled = false;
        }

        if (coinPrefab != null)
        {
            for (int i = 0; i < coinsToSpawn; i++)
            {
                GameObject spawnedCoin = Instantiate(coinPrefab, transform.position, Quaternion.identity);

                CurrencyPickup pickupScript = spawnedCoin.GetComponent<CurrencyPickup>();
                if (pickupScript != null)
                {
                    pickupScript.currencyValue = currencyPerCoin;
                }

                Rigidbody2D rb = spawnedCoin.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    float randomX = Random.Range(-4f, 4f);
                    float randomY = Random.Range(4f, 8f);
                    rb.AddForce(new Vector2(randomX, randomY), ForceMode2D.Impulse);
                }
            }
        }

        Destroy(gameObject);
    }

    private IEnumerator ShakeRoutine()
    {
        isShaking = true;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float xOffset = Random.Range(-shakeMagnitude, shakeMagnitude);
            float yOffset = Random.Range(-shakeMagnitude, shakeMagnitude);

            SetPosition(originalPosition + new Vector3(xOffset, yOffset, 0f));

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetPosition(originalPosition);
        isShaking = false;
        shakeCoroutine = null;
    }

    private void SetPosition(Vector3 targetPosition)
    {
        if (rb != null)
        {
            rb.position = targetPosition;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            return;
        }

        transform.position = targetPosition;
    }
}
