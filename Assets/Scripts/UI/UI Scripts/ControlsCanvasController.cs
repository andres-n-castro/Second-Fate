using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ControlsCanvasController : MonoBehaviour
{
    private const string ModalBlockerName = "Controls Modal Blocker";

    [SerializeField] private Button exitButton;
    private GameObject returnSelection;
    private Image modalBlocker;
    private static SelectionRestorer selectionRestorer;
    private static int activeControlsCount;

    public static bool IsAnyOpen => activeControlsCount > 0;

    private void Awake()
    {
        CacheExitButton();
        EnsureModalBlocker();

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(Close);
        }
    }

    private void OnDestroy()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(Close);
        }
    }

    private void OnEnable()
    {
        activeControlsCount++;
        CacheExitButton();
        EnsureModalBlocker();
        StartCoroutine(SelectExitButtonNextFrame());
    }

    private void OnDisable()
    {
        activeControlsCount = Mathf.Max(0, activeControlsCount - 1);
    }

    private void Update()
    {
        if (EventSystem.current == null || exitButton == null)
        {
            return;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(transform))
        {
            SelectExitButton();
        }
    }

    public void Open(GameObject selectionToRestore)
    {
        SetReturnSelection(selectionToRestore);
        gameObject.SetActive(true);
        SelectExitButton();
        StartCoroutine(SelectExitButtonNextFrame());
    }

    private IEnumerator SelectExitButtonNextFrame()
    {
        yield return null;
        SelectExitButton();
    }

    private void SelectExitButton()
    {
        if (EventSystem.current != null && exitButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(exitButton.gameObject);
        }
    }

    public void Close()
    {
        gameObject.SetActive(false);

        RestoreSelection(returnSelection);
        RestoreSelectionNextFrame(returnSelection);
    }

    private static void RestoreSelection(GameObject selection)
    {
        if (EventSystem.current != null && selection != null && selection.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selection);
        }
    }

    public void SetReturnSelection(GameObject selection)
    {
        returnSelection = selection;
    }

    private void EnsureModalBlocker()
    {
        Transform blockerTransform = transform.Find(ModalBlockerName);

        if (blockerTransform == null)
        {
            GameObject blockerObject = new GameObject(ModalBlockerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            blockerTransform = blockerObject.transform;
            blockerTransform.SetParent(transform, false);
        }

        blockerTransform.SetAsFirstSibling();

        RectTransform blockerRect = blockerTransform as RectTransform;
        if (blockerRect != null)
        {
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
        }

        modalBlocker = blockerTransform.GetComponent<Image>();
        if (modalBlocker == null)
        {
            modalBlocker = blockerTransform.gameObject.AddComponent<Image>();
        }

        modalBlocker.color = Color.clear;
        modalBlocker.raycastTarget = true;
    }

    private void CacheExitButton()
    {
        if (exitButton == null)
        {
            exitButton = GetComponentInChildren<Button>(true);
        }
    }

    private static void RestoreSelectionNextFrame(GameObject selection)
    {
        if (selection == null)
        {
            return;
        }

        if (selectionRestorer == null)
        {
            GameObject restorerObject = new GameObject("UI Selection Restorer");
            selectionRestorer = restorerObject.AddComponent<SelectionRestorer>();
        }

        selectionRestorer.RestoreNextFrame(selection);
    }

    private class SelectionRestorer : MonoBehaviour
    {
        public void RestoreNextFrame(GameObject selection)
        {
            StartCoroutine(Restore(selection));
        }

        private IEnumerator Restore(GameObject selection)
        {
            yield return null;
            RestoreSelection(selection);
        }
    }
}
