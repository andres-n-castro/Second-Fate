using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject controlsCanvasPrefab;
    private GameObject controlsCanvasInstance;

    public void ResumeGame()
    {
        GameManager.Instance.RestorePreviousState();
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("main_menu_scene");
    }

    public void OpenControls()
    {
        if (controlsCanvasInstance == null)
        {
            GameObject existingControls = GameObject.Find("ControlsCanvas");
            if (existingControls != null)
            {
                controlsCanvasInstance = existingControls;
            }
            else if (controlsCanvasPrefab != null)
            {
                controlsCanvasInstance = Instantiate(controlsCanvasPrefab);
            }
        }

        if (controlsCanvasInstance != null)
        {
            ControlsCanvasController controlsCanvasController = controlsCanvasInstance.GetComponent<ControlsCanvasController>();
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            if (controlsCanvasController != null)
            {
                controlsCanvasController.Open(selected);
                return;
            }

            controlsCanvasInstance.SetActive(true);
        }
    }
}
