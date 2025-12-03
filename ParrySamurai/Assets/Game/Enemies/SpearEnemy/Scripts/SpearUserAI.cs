using UnityEngine;
using System.Collections; // We need this for Coroutines

public class SpearUserAI : MonoBehaviour
{
    [Header("AI Settings")]
    [Tooltip("How fast the spear user walks.")]
    public float moveSpeed = 2f;
    [Tooltip("How close the spear user gets to the player before stopping.")]
    public float stoppingDistance = 1.5f;

    // --- NEW: These control the "start-stop" movement ---
    [Header("Burst Movement")]
    [Tooltip("How long the spear user will walk for in one burst.")]
    public float walkDuration = 1.0f;
    [Tooltip("How long the spear user will pause between walks.")]
    public float pauseDuration = 0.5f;

    private Transform playerTarget;
    private Animator animator;
    private Rigidbody2D rb; // We will use a Rigidbody for smoother movement

    // This variable tracks what the AI is currently doing.
    private bool isWalking = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>(); // Get the Rigidbody
    }

    void Start()
    {
        // Find the player
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerTarget = playerObject.transform;
        }
        else
        {
            Debug.LogError("SpearUserAI could not find the 'Player' tag!");
            return;
        }

        // --- NEW: Start the main AI brain loop ---
        StartCoroutine(AIStateRoutine());
    }

    // This is the main "brain" of the AI. It decides when to walk and when to pause.
    private IEnumerator AIStateRoutine()
    {
        // This loop will run forever.
        while (true)
        {
            // First, check the distance to the player.
            float distanceToPlayer = Vector2.Distance(transform.position, playerTarget.position);

            // If we are far from the player, we should try to walk.
            if (distanceToPlayer > stoppingDistance)
            {
                // --- WALK PHASE ---
                isWalking = true;
                animator.SetBool("isWalking", true); // Tell the animator to walk
                FlipTowardsPlayer(); // Make sure we are facing the right way

                // Walk for 'walkDuration' seconds.
                yield return new WaitForSeconds(walkDuration);

                // --- PAUSE PHASE ---
                isWalking = false;
                animator.SetBool("isWalking", false); // Tell the animator to stop walking

                // Pause for 'pauseDuration' seconds.
                yield return new WaitForSeconds(pauseDuration);
            }
            // If we are already close to the player, just wait a moment before checking again.
            else
            {
                isWalking = false;
                animator.SetBool("isWalking", false);
                yield return new WaitForSeconds(0.2f); // Wait a short time
            }
        }
    }

    // We use FixedUpdate for all physics-based movement.
    void FixedUpdate()
    {
        // If the AI is in the "walk" state...
        if (isWalking)
        {
            // ...apply velocity to move towards the player.
            Vector2 direction = (playerTarget.position - transform.position).normalized;
            rb.velocity = new Vector2(direction.x * moveSpeed, rb.velocity.y);
        }
        else
        {
            // If not walking, stop all horizontal movement.
            rb.velocity = new Vector2(0, rb.velocity.y);
        }
    }

    // A simple, reliable flip function that DOES NOT mess with scale.
    private void FlipTowardsPlayer()
    {
        // Determine the direction to the player.
        float directionToPlayer = playerTarget.position.x - transform.position.x;

        // Check if we need to flip.
        // If the player is to the right (direction > 0) AND we are facing left (localScale.x < 0)...
        if (directionToPlayer > 0 && transform.localScale.x < 0)
        {
            // ...then flip to face right.
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        // OR if the player is to the left (direction < 0) AND we are facing right (localScale.x > 0)...
        else if (directionToPlayer < 0 && transform.localScale.x > 0)
        {
            // ...then flip to face left.
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }
}
