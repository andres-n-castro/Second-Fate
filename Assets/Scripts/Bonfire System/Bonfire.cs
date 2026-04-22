using UnityEngine;

public class Bonfire : MonoBehaviour
{
    public string bonfireID;
    [Header("Proximity Popup")]
    [SerializeField] private GameObject proximityPopup;

    private Animator anim;
    private bool isPlayerInRange = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        UpdateVisualState();

        if (proximityPopup != null)
        {
            proximityPopup.SetActive(false);
        }
    }

    void OnEnable()
    {
        GameManager.OnStateChanged += HandleGameStateChanged;
    }

    void OnDisable()
    {
        GameManager.OnStateChanged -= HandleGameStateChanged;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = true;
        UpdatePopupVisibility();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;
        UpdatePopupVisibility();
    }

    void Update()
    {
        bool interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton3);
        if (isPlayerInRange && interactPressed && GameManager.Instance.currentState == GameManager.GameState.Exploration)
        {
            if (proximityPopup != null)
            {
                proximityPopup.SetActive(false);
            }

            GameManager.Instance.lastInteractedBonfireID = bonfireID;
            GameManager.Instance.ChangeState(GameManager.GameState.BonfireMenu);
        }
    }

    private void HandleGameStateChanged(GameManager.GameState state)
    {
        UpdatePopupVisibility();
    }

    private void UpdatePopupVisibility()
    {
        if (proximityPopup == null)
        {
            return;
        }

        bool canShowPopup = isPlayerInRange &&
                            GameManager.Instance != null &&
                            GameManager.Instance.currentState == GameManager.GameState.Exploration;

        proximityPopup.SetActive(canShowPopup);
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
