// CameraFollow.cs

using System.Collections;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The player or object for the camera to follow.")]
    [SerializeField] private Transform target;

    [Header("Movement Settings")]
    [Tooltip("How smoothly the camera follows the target. Lower values are slower and smoother.")]
    [SerializeField] private float smoothSpeed = 0.125f;
    [Tooltip("The offset from the target (e.g., to position the camera slightly above and behind).")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2, -10);

    [Header("Axis Locking")]
    [Tooltip("Check this box to prevent the camera from following the player on the Y-axis.")]
    [SerializeField] private bool lockYAxis = false;

    // This runs after all Update() calls have finished. It's the best place for camera logic
    // to ensure the player has already moved before the camera tries to follow.
    void LateUpdate()
    {
        // Failsafe: If no target is assigned, do nothing to prevent errors.
        if (target == null)
        {
            Debug.LogWarning("Camera Follow: Target not assigned!");
            return;
        }

        // --- 1. Calculate the Desired Position ---
        // Start with the target's position and add the offset.
        Vector3 desiredPosition = target.position + offset;

        // --- 2. Handle Y-Axis Lock ---
        // If the lockYAxis boolean is checked...
        if (lockYAxis)
        {
            // ...then force the camera's desired Y position to be its *current* Y position.
            // This effectively cancels out any vertical movement.
            desiredPosition.y = transform.position.y;
        }

        // --- 3. Smoothly Move the Camera ---
        // Use Vector3.Lerp to smoothly interpolate from the camera's current position
        // to the desired position. The smoothSpeed determines how fast it moves.
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // --- 4. Apply the New Position ---
        transform.position = smoothedPosition;
    }

    // Public method to allow other scripts to change the target at runtime if needed.
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    public void TriggerZoom(float targetZoom, float duration)
    {
        StartCoroutine(ZoomCoroutine(targetZoom, duration));
    }

    private IEnumerator ZoomCoroutine(float targetZoom, float duration)
    {
        // Get the main camera component.
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("CameraFollow: Camera.main is not found! Cannot perform zoom.");
            yield break; // Stop the coroutine if there's no camera.
        }

        float startZoom = cam.orthographicSize;
        float timer = 0f;

        while (timer < duration)
        {
            // Use a smooth Lerp to change the camera's Orthographic Size over time.
            float currentZoom = Mathf.Lerp(startZoom, targetZoom, timer / duration);
            cam.orthographicSize = currentZoom;

            timer += Time.deltaTime;
            yield return null;
        }

        // Ensure the final zoom level is exact.
        cam.orthographicSize = targetZoom;
    }
}
