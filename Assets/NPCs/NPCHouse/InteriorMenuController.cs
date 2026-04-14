using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the three sub-panels inside the full-screen shop overlay:
///   1. Interaction Menu  — Talk / Shop / Leave buttons
///   2. Dialogue Panel    — Typewriter NPC dialogue
///   3. Shop Panel        — Item grid managed by InteriorShopManager
///
/// All coroutines use WaitForSecondsRealtime so they work while the game is paused.
/// </summary>
public class InteriorMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject interactMenuPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject shopPanel;

    [Header("Dialogue UI")]
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image portraitImage;

    [Header("Dialogue Data")]
    [Tooltip("The NPC dialogue ScriptableObject to use for the Talk option.")]
    [SerializeField] private NPCDialogue dialogueData;

    [Header("Typing Settings")]
    [SerializeField] private float typingDelay = 0.03f;
    [SerializeField] private float lineDelay = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource selectAudio;
    [SerializeField] private NPCVoice npcVoice;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.3f;

    [Header("Exit")]
    [Tooltip("Reference to the NPCShopController so the Leave button can close the shop.")]
    [SerializeField] private NPCShopController shopController;

    private Coroutine dialogueCoroutine;

    // ------------------------------------------------------------------
    // Public API — called by UI buttons and NPCShopController
    // ------------------------------------------------------------------

    /// <summary>
    /// Show the interaction menu (Talk / Shop / Leave) and hide everything else.
    /// Called by NPCShopController when the shop is first opened.
    /// </summary>
    public void ShowMenu()
    {
        StopDialogue();

        if (interactMenuPanel != null) interactMenuPanel.SetActive(true);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    /// <summary>
    /// "Talk" button — switch to dialogue panel and start the typewriter.
    /// </summary>
    public void OpenTalk()
    {
        PlaySelect();

        if (interactMenuPanel != null) interactMenuPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        StartDialogue();
    }

    /// <summary>
    /// "Shop" button — switch to the shop grid panel.
    /// </summary>
    public void OpenShop()
    {
        PlaySelect();
        StopDialogue();

        if (interactMenuPanel != null) interactMenuPanel.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(true);
    }

    /// <summary>
    /// "Back" button inside the dialogue or shop panel — return to the menu.
    /// </summary>
    public void BackToMenu()
    {
        PlaySelect();
        ShowMenu();
    }

    /// <summary>
    /// "Leave" button — close the entire shop overlay.
    /// </summary>
    public void Leave()
    {
        PlaySelect();

        if (shopController != null)
            shopController.CloseShop();
    }

    /// <summary>
    /// Called by NPCShopController when the shop is force-closed (e.g. pressing M).
    /// Stops any running dialogue so coroutines don't keep firing.
    /// </summary>
    public void ForceClose()
    {
        StopDialogue();

        if (interactMenuPanel != null) interactMenuPanel.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Dialogue typewriter — uses WaitForSecondsRealtime (works while paused)
    // ------------------------------------------------------------------

    private void StartDialogue()
    {
        StopDialogue();

        if (dialogueData == null) return;

        // Set NPC name and portrait
        if (nameText != null)
            nameText.text = dialogueData.npcName;

        if (portraitImage != null)
            portraitImage.sprite = dialogueData.npcPortrait;

        if (npcVoice != null)
            npcVoice.PlayStartTalk();

        dialogueCoroutine = StartCoroutine(RunDialogue());
    }

    private void StopDialogue()
    {
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
            dialogueCoroutine = null;
        }

        if (npcVoice != null)
            npcVoice.StopAll();

        if (dialogueText != null)
            dialogueText.text = "";
    }

    private IEnumerator RunDialogue()
    {
        if (dialogueData == null || dialogueData.dialogueLines == null)
            yield break;

        for (int i = 0; i < dialogueData.dialogueLines.Length; i++)
        {
            yield return StartCoroutine(TypeLine(dialogueData.dialogueLines[i]));
            yield return new WaitForSecondsRealtime(lineDelay);
        }

        // After all lines finish, return to the interaction menu
        ShowMenu();
    }

    private IEnumerator TypeLine(string line)
    {
        if (dialogueText != null)
            dialogueText.text = "";

        for (int i = 0; i < line.Length; i++)
        {
            if (dialogueText != null)
                dialogueText.text += line[i];

            if (npcVoice != null && line[i] != ' ')
                npcVoice.Blip();

            yield return new WaitForSecondsRealtime(typingDelay);
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private void PlaySelect()
    {
        if (selectAudio != null && selectAudio.clip != null && selectAudio.enabled && selectAudio.gameObject.activeInHierarchy)
        {
            selectAudio.PlayOneShot(selectAudio.clip, sfxVolume);
        }
    }
}