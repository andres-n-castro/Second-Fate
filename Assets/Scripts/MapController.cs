using UnityEngine;

public sealed class MapController : MonoBehaviour
{
    [SerializeField] private GameObject mapOverlay; // Drag your UI Raw Image here
    [SerializeField] private KeyCode mapKey = KeyCode.M;

    void Start()
    {
        // Ensure map is hidden at start
        if (mapOverlay != null) mapOverlay.SetActive(false);
    }

    void Update()
    {
        // Detects if the key is being held down
        if (Input.GetKey(mapKey))
        {
            mapOverlay.SetActive(true);
        }
        else
        {
            mapOverlay.SetActive(false);
        }
    }
}