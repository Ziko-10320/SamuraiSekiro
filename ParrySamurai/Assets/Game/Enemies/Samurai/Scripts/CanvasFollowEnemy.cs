using UnityEngine;

public class CanvasFollowEnemy : MonoBehaviour
{
    [SerializeField] private Transform enemyTransform;  // The enemy this canvas follows
    [SerializeField] private Vector3 offset = new Vector3(0, 2, 0);  // Offset above enemy
    [SerializeField] private bool faceCamera = true;  // Make canvas face camera

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (enemyTransform == null) return;

        // NEW: Update position to follow enemy
        transform.position = enemyTransform.position + offset;

        // NEW: Make canvas face the camera (so it's always readable)
        if (faceCamera && mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.forward);
        }

        Debug.Log($"Canvas Position: {transform.position}, Enemy Position: {enemyTransform.position}");
    }
}