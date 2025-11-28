// EnemyFollow.cs (FINAL - Simple & Reliable with Root Motion)

using UnityEngine;
using System.Collections;

public class EnemyFollow : MonoBehaviour
{
    private enum EnemyState { Idle, Following }
    private EnemyState currentState;

    [Header("Targeting")]
    [Tooltip("The player's transform that the enemy will follow.")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float moveSpeed = 3f;
    [Header("Movement")]
    [Tooltip("The distance from the target point at which the enemy will stop.")]
    [SerializeField] private float stoppingDistance = 1.5f;
    [Tooltip("An empty GameObject used as the reference point for stopping distance.")]
    [SerializeField] private Transform stoppingPoint;

    [Header("Flip Settings")]
    [Tooltip("The rotation to apply when the enemy is facing right.")]
    [SerializeField] private Vector3 rightFacingRotation = new Vector3(0, -137, 0);
    [Tooltip("The rotation to apply when the enemy is facing left.")]
    [SerializeField] private Vector3 leftFacingRotation = new Vector3(0, -222, 180);
    [Tooltip("The vertical offset to apply to the enemy's Y-scale when flipping.")]
    [SerializeField] private float yFlipScale = -1f;
    private bool isFacingRight = true;

    [Header("Components")]
    private Animator animator;
    private Rigidbody2D rb;

    private readonly int isWalkingHash = Animator.StringToHash("isWalking");
    private Coroutine decelerationCoroutine;
    [SerializeField] private float decelerationDuration = 0.2f;
    // This variable will control if velocity is applied.
    private bool canMove = false;
    [Header("Lunge Settings")]
    [SerializeField] private float lungeSpeed = 6f;
    [SerializeField] private float lungeDuration = 0.2f;
    private bool isLunging = false;
    private float lungeTimer = 0f;
    private Vector2 lungeDirection;
    private EnemyHealth healthScript;
    [Header("Defensive Dash")]
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float dashDuration = 0.4f;
    private bool isDashing = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        healthScript = GetComponent<EnemyHealth>();
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
                enabled = false;
            }
        }
    }

    void Update()
    {
        if (isDashing || (healthScript != null && healthScript.IsStunned()))
        {
            return;
        }
        if (isLunging)
        {
            lungeTimer -= Time.deltaTime;
            if (lungeTimer <= 0)
            {
                isLunging = false;
                rb.velocity = Vector2.zero; // Stop the lunge movement.
            }
            return; // IMPORTANT: If we are lunging, don't do any other movement logic.
        }
        if (GetComponent<EnemyHealth>().IsStunned())
        {
            // When stunned, ensure the walking animation is off.
            animator.SetBool(isWalkingHash, false);
            return; // Stop here. Do not run any follow or flip logic.
        }
        if (playerTarget == null) return;

        // Determine the distance to the target.
        float distanceToTarget = Vector2.Distance(new Vector2(transform.position.x, 0), new Vector2(GetTargetPosition().x, 0));

        // Update the state based on distance.
        if (distanceToTarget > stoppingDistance)
        {
            currentState = EnemyState.Following;
        }
        else
        {
            currentState = EnemyState.Idle;
        }

        // Update the animator based on the state.
        animator.SetBool(isWalkingHash, currentState == EnemyState.Following);

        // Force the flip every frame to ensure it's always correct.
        FlipTowardsPlayer();
    }
    void FixedUpdate()
    {
        if (isLunging)
        {
            // If we are lunging, apply the lunge velocity.
            rb.velocity = lungeDirection * lungeSpeed;
            return; // IMPORTANT: Stop here so normal movement doesn't interfere.
        }
        // If a deceleration coroutine is running, let it handle the velocity.
        if (decelerationCoroutine != null)
        {
            return;
        }

        if (canMove && currentState == EnemyState.Following)
        {
            // Calculate the direction to the target.
            Vector2 direction = (GetTargetPosition() - (Vector2)transform.position).normalized;
            // Apply velocity.
            rb.velocity = new Vector2(direction.x * moveSpeed, rb.velocity.y);
        }
        else
        {
            // If not allowed to move, velocity is zero.
            rb.velocity = new Vector2(0, rb.velocity.y);
        }
    }
    private void FlipTowardsPlayer()
    {
        float directionToPlayer = playerTarget.position.x - transform.position.x;
        if (directionToPlayer > 0 && !isFacingRight)
        {
            isFacingRight = true;
        }
        else if (directionToPlayer < 0 && isFacingRight)
        {
            isFacingRight = false;
        }

        // Apply the correct rotation and scale every frame.
        transform.rotation = Quaternion.Euler(isFacingRight ? rightFacingRotation : leftFacingRotation);
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), isFacingRight ? 2 : yFlipScale, transform.localScale.z);
    }
    public void TriggerDefensiveDash()
    {
        if (!isDashing)
        {
            StartCoroutine(DefensiveDashCoroutine());
        }
    }

    private IEnumerator DefensiveDashCoroutine()
    {
        isDashing = true;

        // Calculate the direction AWAY from the player.
        Vector2 directionToPlayer = (playerTarget.position - transform.position).normalized;
        Vector2 dashDirection = -directionToPlayer;

        float timer = 0f;
        while (timer < dashDuration)
        {
            rb.MovePosition(rb.position + dashDirection * dashSpeed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }

        isDashing = false;
    }
    private Vector2 GetTargetPosition()
    {
        if (stoppingPoint != null)
        {
            return (Vector2)playerTarget.position + (Vector2)stoppingPoint.localPosition;
        }
        else
        {
            return playerTarget.position;
        }
    }

    public void StartMovement()
    {
        if (decelerationCoroutine != null)
        {
            StopCoroutine(decelerationCoroutine);
            decelerationCoroutine = null;
        }
        canMove = true;
    }

    public void StopMovement()
    {
        canMove = false;
        if (gameObject.activeInHierarchy && decelerationCoroutine == null)
        {
            decelerationCoroutine = StartCoroutine(DecelerateCoroutine());
        }
    }
    public void PerformLunge()
    {
        // We don't need a coroutine here, we can use the existing 'isLunging' state.
        isLunging = true;
        lungeTimer = lungeDuration;

        // Determine lunge direction based on where the enemy is facing.
        lungeDirection = isFacingRight ? Vector2.right : Vector2.left;
    }
    public bool IsFacingRight()
    {
        return isFacingRight;
    }
    private IEnumerator DecelerateCoroutine()
    {
        float timer = 0f;
        Vector2 startVelocity = rb.velocity;

        while (timer < decelerationDuration)
        {
            rb.velocity = Vector2.Lerp(startVelocity, new Vector2(0, startVelocity.y), timer / decelerationDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        rb.velocity = new Vector2(0, rb.velocity.y);
        decelerationCoroutine = null;
    }
}
