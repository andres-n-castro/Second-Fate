using UnityEngine;

public class InteriorMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject interactMenuPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject shopPanel;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource selectAudio;

    void Start()
    {
        ShowMenu();
    }

    public void ShowMenu()
    {
        if (interactMenuPanel != null) interactMenuPanel.SetActive(true);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    public void OpenTalk()
    {
        PlaySelect();
        if (interactMenuPanel != null) interactMenuPanel.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    public void OpenShop()
    {
        PlaySelect();
        if (interactMenuPanel != null) interactMenuPanel.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(true);
    }

    public void CloseDialogue()
    {
        PlaySelect();
        ShowMenu();
    }

    public void CloseShop()
    {
        PlaySelect();
        ShowMenu();
    }

    private void PlaySelect()
    {
        if (selectAudio != null && selectAudio.enabled && selectAudio.gameObject.activeInHierarchy)
        {
            selectAudio.Play();
        }
    }
}