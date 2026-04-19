using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Settings")]
    public GameObject enemyPrefab;
    public bool isBoss = false;
    public string bossID = "";

    private GameObject currentEnemyInstance;

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
        if (isBoss && SaveManager.Instance != null && SaveManager.Instance.currentSaveData.defeatedBossIDs.Contains(bossID))
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
