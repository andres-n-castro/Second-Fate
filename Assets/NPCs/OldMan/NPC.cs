using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NPC : MonoBehaviour
{
    [Header("Dialogue Data/UI")]
    public NPCDialogue dialogueData;
    public GameObject dialoguePanel;
    public TMP_Text dialogueText;
    public TMP_Text nameText;
    public Image portraitImage;

    [Header("Interaction")]
    [SerializeField] private InteractionMenuUI menuUI;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool endDialogueOnExit = false;

    [Header("Audio")]
    private NPCVoice voice;

    [Header("Typing")]
    [SerializeField] private float typingDelay = 0.03f;
    [SerializeField] private float lineDelay = 1.0f;

    int dialogueIndex;
    bool isDialogueActive;
    bool playerInRange;

   void Awake()
    {
        voice = GetComponent<NPCVoice>();
        
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = true;

        if (menuUI != null)
            menuUI.SetNearbyNPC(this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = false;

        if (menuUI != null)
            menuUI.ClearNPC();

        if (endDialogueOnExit && isDialogueActive)
            EndDialogue();

        CloseShop();
    }

    public void StartTalk()
    {
        if (dialogueData == null) return;
        if (isDialogueActive) return;

        CloseShop();

        isDialogueActive = true;
        dialogueIndex = 0;

        if (voice != null) voice.PlayStartTalk();

        if (nameText != null)
            nameText.SetText(dialogueData.npcName);

        if (portraitImage != null)
            portraitImage.sprite = dialogueData.npcPortrait;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(RunDialogue());
    }

    public void OpenShop()
    {
        EndDialogue();

        if (shopPanel != null)
            shopPanel.SetActive(true);
    }

    public void CloseShop()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (menuUI != null && playerInRange)
            menuUI.SetNearbyNPC(this);
    }

    IEnumerator RunDialogue()
    {
        while (dialogueIndex < dialogueData.dialogueLines.Length)
        {
            yield return StartCoroutine(TypeLine(dialogueData.dialogueLines[dialogueIndex]));
            yield return new WaitForSeconds(lineDelay);
            dialogueIndex++;
        }

        EndDialogue();
    }

    IEnumerator TypeLine(string line)
    {
        if (dialogueText != null)
            dialogueText.SetText("");

        for (int i = 0; i < line.Length; i++)
        {
            if (dialogueText != null)
                dialogueText.text += line[i];

            if (voice != null && line[i] != ' ')
                voice.Blip();

            yield return new WaitForSeconds(typingDelay);
        }
    }

    public void EndDialogue()
    {
        StopAllCoroutines();
        isDialogueActive = false;

        if (voice != null) voice.StopAll();

        if (dialogueText != null) dialogueText.SetText("");
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        if (menuUI != null && playerInRange)
            menuUI.SetNearbyNPC(this);
    }

    public bool IsBusy()
    {
        bool dialogueOpen = dialoguePanel != null && dialoguePanel.activeSelf;
        bool shopOpen = shopPanel != null && shopPanel.activeSelf;
        bool menuOpen = menuUI != null && menuUI.IsMenuOpen();
        return dialogueOpen || shopOpen || menuOpen || isDialogueActive;
    }

    public bool BlocksMovement()
    {
        return IsBusy();
    }
}