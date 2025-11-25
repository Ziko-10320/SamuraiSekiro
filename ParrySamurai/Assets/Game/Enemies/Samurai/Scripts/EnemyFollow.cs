using UnityEngine;

public class EnemyFollow : MonoBehaviour
{
    private enum EnemyState { Idle, Following }
    private EnemyState currentState;

    [Header("Targeting")]
    [Tooltip("The player's transform that the enemy will follow.")]
    [SerializeField] private Transform playerTarget;

    [Header("Movement")]
    [Tooltip("How fast the enemy moves towards the player.")]
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("The distance from the target point at which the enemy will stop.")]
    [SerializeField] private float stoppingDistance = 1.5f;
    [Tooltip("An empty GameObject used as the reference point for stopping distance. The enemy will try to stop at this point relative to the player.")]
    [SerializeField] private Transform stoppingPoint;
    [Tooltip("The vertical offset to apply to the enemy's position. Use this to fine-tune its Y position relative to the player.")]
    [SerializeField] private float yPositionOffset = 0f;

    [Header("Flip Settings")]
    [Tooltip("The rotation to apply when the enemy is facing right.")]
    [SerializeField] private Vector3 rightFacingRotation = new Vector3(0, -137, 0);
    [Tooltip("The rotation to apply when the enemy is facing left.")]
    [SerializeField] private Vector3 leftFacingRotation = new Vector3(0, -222, 180);
    private bool isFacingRight = true;

    [Header("Components")]
    private Animator animator;

    // --- Animation Hashes ---
    private readonly int isWalkingHash = Animator.StringToHash("isWalking");

    void Awake()
    {
        animator = GetComponent<Animator>();

        // Failsafe: Try to find the player automatically if not assigned.
        if (playerTarget == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerTarget = playerObject.transform;
            }
            else
            {
                Debug.LogError("EnemyFollow: Player target not found! Please assign the player or tag them as 'Player'.", this);
                enabled = false; // Disable the script if there's no player to follow.
            }
        }
    }

    void Update()
    {
        if (playerTarget == null) return;

        // --- State Logic ---
        // Calculate the distance to the target stopping point.
        float distanceToTarget = Vector2.Distance(transform.position, GetTargetPosition());

        // Decide if we should be idle or following.
        if (distanceToTarget > stoppingDistance)
        {
            currentState = EnemyState.Following;
        }
        else
        {
            currentState = EnemyState.Idle;
        }

        // --- Action Logic ---
        if (currentState == EnemyState.Following)
        {
            MoveTowardsPlayer();
            animator.SetBool(isWalkingHash, true);
        }
        else // If Idle
        {
            animator.SetBool(isWalkingHash, false);
        }

        // Always face the player, regardless of state.
        FlipTowardsPlayer();
    }

    /// <summary>
    /// Calculates the final destination for the enemy.
    /// </summary>
    private Vector2 GetTargetPosition()
    {
        // If a stoppingPoint transform is assigned, use its position relative to the player.
        if (stoppingPoint != null)
        {
            // This is the key: It gets the stopping point's local position and adds it to the player's world position.
            return (Vector2)playerTarget.position + (Vector2)stoppingPoint.localPosition;
        }
        else
        {
            // If no stopping point is assigned, just target the player's base position.
            return playerTarget.position;
        }
    }

    private void MoveTowardsPlayer()
    {
        // Calculate the target position, including the Y offset.
        Vector2 finalTargetPosition = GetTargetPosition();
        finalTargetPosition.y += yPositionOffset;

        // Use MoveTowards for smooth, consistent movement that doesn't overshoot.
        transform.position = Vector2.MoveTowards(transform.position, finalTargetPosition, moveSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Handles the flipping logic, including rotation and Y-scale, similar to the player.
    /// </summary>
    private void FlipTowardsPlayer()
    {
        // Determine the direction to the player.
        float directionToPlayer = playerTarget.position.x - transform.position.x;

        // If the player is to the right, but we are facing left...
        if (directionToPlayer > 0 && !isFacingRight)
        {
            // ...flip to face RIGHT.
            transform.rotation = Quaternion.Euler(rightFacingRotation);
            transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y, transform.localScale.z);
            isFacingRight = true;
        }
        // If the player is to the left, but we are facing right...
        else if (directionToPlayer < 0 && isFacingRight)
        {
            // ...flip to face LEFT.
            transform.rotation = Quaternion.Euler(leftFacingRotation);
            transform.localScale = new Vector3(transform.localScale.x, -transform.localScale.y, transform.localScale.z);
            isFacingRight = false;
        }
    }
}

