using UnityEngine;

public class TutorialKey : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            TutorialManager.Instance.hasTutorialKey = true;
            Debug.Log("Key Picked Up");

            Destroy(gameObject);
        }
    }
}
