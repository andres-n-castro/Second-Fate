using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    public void ResumeGame()
    {
        GameManager.Instance.RestorePreviousState();
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("main_menu_scene");
    }
}
