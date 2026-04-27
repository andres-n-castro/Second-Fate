using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ControlsCanvasController : MonoBehaviour
{
    [SerializeField] private Button exitButton;
    private GameObject returnSelection;
    private static SelectionRestorer selectionRestorer;

    private void Awake()
    {
        CacheExitButton();

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
        CacheExitButton();
        StartCoroutine(SelectExitButtonNextFrame());
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
