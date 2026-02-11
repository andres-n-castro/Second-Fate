using UnityEngine;

public class LockerDoor : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (TutorialManager.Instance.hasTutorialKey)
            {
                UnlockDoor();
            }
            else
            {
                Debug.Log("The locker is locked. Finish tutorial");
            }
        }
    }

    void UnlockDoor()
    {
        gameObject.SetActive(false);
    }
}
