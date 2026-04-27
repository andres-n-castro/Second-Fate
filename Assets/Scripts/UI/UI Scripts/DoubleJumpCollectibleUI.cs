using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DoubleJumpCollectibleUI : MonoBehaviour
{
    [SerializeField] private Button closeButton;
    private bool appliedExternalFreeze;

    private void Awake()
    {
        CacheCloseButton();

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }

    private void OnDisable()
    {
        ReleaseWorldFreeze();
    }

    private void OnEnable()
    {
        ApplyWorldFreeze();

        CacheCloseButton();
        if (EventSystem.current != null && closeButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }
    }

    public void Close()
    {
        ReleaseWorldFreeze();
        gameObject.SetActive(false);
    }

    private void CacheCloseButton()
    {
        if (closeButton == null)
        {
            closeButton = GetComponentInChildren<Button>(true);
        }
    }

    private void ApplyWorldFreeze()
    {
        appliedExternalFreeze = true;

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.SetExternalFreeze(true);
        }
        else
        {
            Time.timeScale = 0f;
        }
    }

    private void ReleaseWorldFreeze()
    {
        if (!appliedExternalFreeze) return;

        appliedExternalFreeze = false;

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.SetExternalFreeze(false);
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
}
