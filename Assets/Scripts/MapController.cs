using UnityEngine;

public sealed class MapController : MonoBehaviour
{
    [SerializeField] private GameObject mapOverlay; // Drag your UI Raw Image here
    [SerializeField] private KeyCode mapKey = KeyCode.M;
    [SerializeField] private KeyCode controllerMapButton = KeyCode.JoystickButton13;

    void Start()
    {
        // Ensure map is hidden at start
        if (mapOverlay != null) mapOverlay.SetActive(false);
    }

    void Update()
    {
        if (mapOverlay == null) return;

        // Hold keyboard M or the PlayStation touchpad click to show the minimap.
        if (Input.GetKey(mapKey) || Input.GetKey(controllerMapButton))
        {
            mapOverlay.SetActive(true);
        }
        else
        {
            mapOverlay.SetActive(false);
        }
    }
}