using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{
    public float parallaxFactor; // 0 is far away (moves with camera), 1 is stationary
    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void LateUpdate()
    {
        // Calculate how far the camera has moved from the start
        Vector3 cameraDelta = Camera.main.transform.position;

        // Apply the parallax only to the movement
        // A factor of 0.1 means it only follows 10% of the camera's movement
        transform.position = new Vector3(
            startPosition.x + (cameraDelta.x * parallaxFactor),
            startPosition.y + (cameraDelta.y * (parallaxFactor * 0.5f)), // Halve vertical parallax
            transform.position.z
        );
    }
}
