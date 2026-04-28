using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Settings")]
    public GameObject enemyPrefab;
    public bool isBoss = false;
    public string bossID = "";

    private GameObject currentEnemyInstance;
    private string RuntimeBossID => !string.IsNullOrEmpty(bossID)
        ? bossID
        : $"{gameObject.scene.name}:{(enemyPrefab != null ? enemyPrefab.name : gameObject.name)}";

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnWorldReset += HandleWorldReset;
        }

        SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        if (isBoss && SaveManager.Instance != null && SaveManager.Instance.IsBossDefeated(RuntimeBossID))
        {
            return;
        }

        if (currentEnemyInstance != null)
        {
            Destroy(currentEnemyInstance);
        }

        if (enemyPrefab != null)
        {
            currentEnemyInstance = Instantiate(enemyPrefab, transform.position, transform.rotation);
        }
    }

    private void HandleWorldReset()
    {
        SpawnEnemy();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnWorldReset -= HandleWorldReset;
        }
    }
}
