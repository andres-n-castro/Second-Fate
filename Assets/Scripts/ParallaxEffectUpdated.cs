using UnityEngine;

public class ParallaxEffectUpdated : MonoBehaviour
{
    public float parallaxFactor; // 0 is stationary, 1 moves exactly with camera

    private Transform cam;
    private Vector3 previousCameraPosition;

    void Start()
    {
        cam = Camera.main.transform;
        previousCameraPosition = cam.position;
    }

    void LateUpdate()
    {
        // Calculate movement since the last frame
        Vector3 cameraDelta = cam.position - previousCameraPosition;

        // THE MAGIC SHIELD: 
        // If the camera moves more than 10 units in a single frame, it's a teleport/snap.
        // We only apply parallax if the movement is normal, smooth player movement.
        if (cameraDelta.magnitude < 10f)
        {
            transform.position += new Vector3(
                cameraDelta.x * parallaxFactor,
                cameraDelta.y * (parallaxFactor * 0.5f),
                0f // Leave Z alone
            );
        }

        // Always update the previous position so we are ready for the next frame
        previousCameraPosition = cam.position;
    }
}