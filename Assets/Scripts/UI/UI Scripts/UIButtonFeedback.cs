using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float scaleFactor = 1.1f;
    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    private void OnDisable()
    {
        transform.localScale = originalScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => transform.localScale = originalScale * scaleFactor;
    public void OnPointerExit(PointerEventData eventData) => transform.localScale = originalScale;
    public void OnSelect(BaseEventData eventData) => transform.localScale = originalScale * scaleFactor;
    public void OnDeselect(BaseEventData eventData) => transform.localScale = originalScale;
}
