using UnityEngine;

public class Bonfire : MonoBehaviour
{
    public string bonfireID;
    private Animator anim;
    private bool isPlayerInRange = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        UpdateVisualState();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) isPlayerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) isPlayerInRange = false;
    }

    void Update()
    {
        bool interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton3);
        if (isPlayerInRange && interactPressed && GameManager.Instance.currentState == GameManager.GameState.Exploration)
        {
            GameManager.Instance.lastInteractedBonfireID = bonfireID;
            GameManager.Instance.ChangeState(GameManager.GameState.BonfireMenu);
        }
    }

    public void UpdateVisualState()
    {
        if (anim == null) return;

        GameManager.AlignmentType currentAlignment = GameManager.Instance.GetBonfireAlignment(bonfireID);
        if (currentAlignment == GameManager.AlignmentType.None) anim.SetInteger("BonfireState", 0);
        else if (currentAlignment == GameManager.AlignmentType.TreeEssence) anim.SetInteger("BonfireState", 1);
        else if (currentAlignment == GameManager.AlignmentType.CreatureBlood) anim.SetInteger("BonfireState", 2);
    }
}
