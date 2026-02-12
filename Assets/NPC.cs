using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NPC : MonoBehaviour
{
    public NPCDialogue dialogueData;
    public GameObject dialoguePanel;
    public TMP_Text dialogueText;
    public TMP_Text nameText;
    public Image portraitImage;

    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool endDialogueOnExit = false;

    [SerializeField] private float typingDelay = 0.03f;
    [SerializeField] private float lineDelay = 1.0f;

    private int dialogueIndex;
    private bool isDialogueActive;

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (dialogueData == null) return;
        if (isDialogueActive) return;
        StartDialogue();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (endDialogueOnExit && isDialogueActive)
            EndDialogue();
    }

    private void StartDialogue()
    {
        isDialogueActive = true;
        dialogueIndex = 0;

        if (nameText != null)
            nameText.SetText(dialogueData.name);

        if (portraitImage != null)
            portraitImage.sprite = dialogueData.npcPortrait;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(RunDialogue());
    }

    private IEnumerator RunDialogue()
    {
        while (dialogueIndex < dialogueData.dialogueLines.Length)
        {
            yield return StartCoroutine(TypeLine(dialogueData.dialogueLines[dialogueIndex]));
            yield return new WaitForSeconds(lineDelay);
            dialogueIndex++;
        }

        EndDialogue();
    }

    private IEnumerator TypeLine(string line)
    {
        if (dialogueText != null)
            dialogueText.SetText("");

        for (int i = 0; i < line.Length; i++)
        {
            if (dialogueText != null)
                dialogueText.text += line[i];

            yield return new WaitForSeconds(typingDelay);
        }
    }

    public void EndDialogue()
    {
        StopAllCoroutines();
        isDialogueActive = false;

        if (dialogueText != null)
            dialogueText.SetText("");

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }
}
