using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{
    [Tooltip("The speed multiplier for the parallax effect. Higher values mean the object moves faster (closer to the camera).")]
    public float parallaxSpeed = 0.5f;

    private Transform cameraTransform;
    private Vector3 startPosition;
    private float startZ;

    void Start()
    {
        // Find the main camera's transform
        cameraTransform = Camera.main.transform;

        // Store the starting position of the background object
        startPosition = transform.position;

        // Store the camera's starting Z position (for calculation stability)
        startZ = cameraTransform.position.z;
    }

    void LateUpdate()
    {
        // Calculate the distance the camera has moved from its starting X position.
        // This is the key to a stable parallax effect.
        float distance = cameraTransform.position.x * parallaxSpeed;

        // Calculate the new position for the background object.
        // We only apply the parallax movement to the X-axis.
        // The new position is based on the object's original position + the calculated distance.
        Vector3 newPosition = new Vector3(startPosition.x + distance, startPosition.y, startPosition.z);

        // Set the object's position.
        transform.position = newPosition;
    }
}
