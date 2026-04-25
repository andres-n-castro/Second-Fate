using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTeleporter : MonoBehaviour
{
    [SerializeField] private string sceneToLoad;
    [SerializeField] private GameObject interactionPrompt;
    private bool _isPlayerInZone = false;
    private const KeyCode PortalInteractButton = KeyCode.JoystickButton3;

    private void Start()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }

    void Update()
    {
        bool interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(PortalInteractButton);
        if (_isPlayerInZone && interactPressed)
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInZone = true;
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInZone = false;
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
    }
}
