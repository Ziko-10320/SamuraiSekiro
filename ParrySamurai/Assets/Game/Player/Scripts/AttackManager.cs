// AttackManager.cs (FINAL - Works with Kinematic Root Motion)

using UnityEngine;
using System.Collections;
using Cinemachine;

public class AttackManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private ZreyMovements playerMovement;

    [Header("Effects")]
    [SerializeField] private ParticleSystem slashEffect1;
    [SerializeField] private ParticleSystem slashEffect2;

    [Header("Combo Settings")]
    private int comboStep = 0;
    private bool isAttacking = false;
    private bool attackQueued = false;

    [Header("Attack Movement")]
    [Tooltip("The speed of the lunge during an attack.")]
    [SerializeField] private float lungeSpeed = 8f;
    [Tooltip("How long the lunge lasts (in seconds).")]
    [SerializeField] private float lungeDuration = 0.15f;

    [SerializeField] private float attackLockoutDuration = 1.5f;

    // --- State ---
    
    private bool isAttackLocked = false;
    private int consecutiveParries = 0; // NEW: Tracks how many times we've been parried.
    private bool isAttackDisabled = false;

    // --- Animation Hashes ---
    private readonly int attack1TriggerHash = Animator.StringToHash("attack1");
    private readonly int attack2TriggerHash = Animator.StringToHash("attack2");
    private Rigidbody2D rb;

    [Header("Damage Settings")]
    [Tooltip("The amount of damage the player's attacks deal.")]
    [SerializeField] private int attackDamage = 25;
    [Tooltip("An empty GameObject marking the center of the player's damage area.")]
    [SerializeField] private Transform attackDamagePoint;
    [Tooltip("The size of the damage area (Width, Height).")]
    [SerializeField] private Vector2 attackDamageAreaSize = new Vector2(1.5f, 1f);
    [Tooltip("The layer the enemies are on, so we know who to damage.")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private PlayerHealth playerHealth;
    [Header("Guard Break Settings")]
    [Tooltip("How many times the player must be parried to have their guard broken.")]
    [SerializeField] private int parriesUntilGuardBreak = 3;
    [Tooltip("How long the player cannot attack after a guard break (in seconds).")]
    [SerializeField]private float guardBreakAttackLockout = 1.5f;

    // --- State Control ---
    private bool isDamageFrameActive = false;
    private bool hasDealtDamageThisAttack = false;

    [Header("Finisher Settings")]
    [Tooltip("The point from which we scan for finishable enemies.")]
    [SerializeField] private Transform finisherCheckPoint; 
    [Tooltip("The radius of the scan for finishable enemies.")]
    [SerializeField] private float finisherCheckRadius = 1.5f; 
    [Tooltip("The layer the enemies are on.")]
    [SerializeField] private LayerMask finisherEnemyLayer; // This should be your normal 'enemyLayer'"

private bool isPerformingFinisher = false;
    private EnemyHealth currentFinisherTarget;
    // --- ADD THESE NEW ANIMATION HASHES ---
    private readonly int performFinisherTriggerHash = Animator.StringToHash("PerformFinisher");
    [Header("Finisher Camera Settings")]
    [Tooltip("The Cinemachine Virtual Camera that follows the player.")]
    [SerializeField] private CameraFollow cameraFollowScript; 
    [Tooltip("The target zoom level (Orthographic Size) for the camera during the finisher.")]
    [SerializeField] private float finisherZoomLevel = 4f; 
    [Tooltip("How fast the camera zooms in and out (in seconds).")]
    [SerializeField] private float zoomDuration = 0.5f; 
    private float originalZoomLevel;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (playerMovement == null) playerMovement = GetComponent<ZreyMovements>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (isPerformingFinisher)
        {
            return;
        }
        if (isAttackDisabled)
        {
            return;
        }

        // --- THIS IS THE FINAL, GUARANTEED FIX ---
        // We add ONE condition: !playerHealth.isBlocking
        // This prevents an attack from being queued while the player is holding the block button.
        if (Input.GetMouseButtonDown(0) && playerMovement.IsGrounded() && !playerMovement.IsDashing())
        {
            // --- THIS IS THE NEW FINISHER LOGIC ---
            // 1. Try to perform a finisher first.
            bool finisherFound = TryPerformFinisher();

            // 2. If a finisher was NOT found, then queue a normal attack.
            if (!finisherFound && (playerHealth == null || !playerHealth.isBlocking))
            {
                attackQueued = true;
            }
            // --- END OF NEW LOGIC ---
        }
        // --- END OF FINAL, GUARANTEED FIX ---

        HandleAttacks();
    }
    private bool TryPerformFinisher()
    {
        // Scan a circle in front of the player for any colliders on the enemy layer.
        Collider2D enemyToFinish = Physics2D.OverlapCircle(finisherCheckPoint.position, finisherCheckRadius, finisherEnemyLayer);

        // If we didn't find an enemy, we can't perform a finisher.
        if (enemyToFinish == null)
        {
            return false;
        }

        // We found an enemy. Now, ask it if it's finishable.
        EnemyHealth enemyHealth = enemyToFinish.GetComponent<EnemyHealth>();
        if (enemyHealth != null && enemyHealth.IsFinishable())
        {
            // SUCCESS! Start the finisher sequence.
            StartCoroutine(FinisherSequence(enemyHealth));
            return true; // Return true to prevent a normal attack.
        }

        // We found an enemy, but it wasn't finishable.
        return false;
    }

    private IEnumerator FinisherSequence(EnemyHealth targetEnemy)
    {
        Debug.Log("--- FINISHER SEQUENCE STARTED ---");
        isPerformingFinisher = true; // Lock the player's AttackManager.
        currentFinisherTarget = targetEnemy;
        // 1. The Lockdown
        playerMovement.SetAttacking(true); // Re-use this to lock player movement.
        if (cameraFollowScript != null && Camera.main != null)
        {
            // 1. Store the camera's current zoom level.
            originalZoomLevel = Camera.main.orthographicSize;
            // 2. Command the CameraFollow script to zoom in.
            cameraFollowScript.TriggerZoom(finisherZoomLevel, zoomDuration);
        }
        // 2. The "Warp"
        // This instantly moves the player to the perfect position relative to the enemy.
        // You MUST adjust these values to fit your animations.
        Vector3 finisherPosition = targetEnemy.transform.position + (targetEnemy.transform.right * -1.2f); // Example: 1.2 units in front of the enemy
        transform.position = finisherPosition;
        // Force the player to look at the enemy.
        playerMovement.FlipTowards(targetEnemy.transform);

        // 3. The "Action!" Call
        // Tell the player's animator to play the finisher.
        animator.SetTrigger(performFinisherTriggerHash);
        // Tell the enemy's animator to play its reaction.
        targetEnemy.GetComponent<Animator>().SetTrigger("ReceiveFinisher"); // We will create this trigger.

        // The animation event will handle the rest.
        yield return null; // The coroutine just starts the process.
    }

    // This method will be called by an Animation Event at the end of the finisher animation.
    public void FinishFinisher(EnemyHealth targetEnemy)
    {
        if (currentFinisherTarget == null)
        {
            Debug.LogError("FinishFinisher was called, but there is no currentFinisherTarget!", this);
            // We still need to unlock the player even if something went wrong.
            isPerformingFinisher = false;
            playerMovement.SetAttacking(false);
            return;
        }
        // --- END OF FIX ---
        if (cameraFollowScript != null)
        {
            // Command the CameraFollow script to zoom back out to the original level.
            cameraFollowScript.TriggerZoom(originalZoomLevel, zoomDuration);
        }
        Debug.Log("--- FINISHER SEQUENCE FINISHED ---");

        // 2. Execute the death of the stored target.
        currentFinisherTarget.ExecuteDeath();

        // 3. Clear the stored target so we don't accidentally kill it again.
        currentFinisherTarget = null;

        // 4. Release the locks on the player.
        isPerformingFinisher = false;
        playerMovement.SetAttacking(false);
    }
  
    public void CancelAttack()
    {
        // This method is called when the player gets hit.
        // It resets all the attack-related states.
        isAttacking = false;
        attackQueued = false;
        comboStep = 0;

        // This is the most important line: it unlocks the movement script.
        if (playerMovement != null)
        {
            playerMovement.SetAttacking(false); // This sets isAttackLocked = false
        }

        // You might also want to stop the attack animation immediately.
        // This prevents the player from sliding while in the "hurt" animation.
        animator.Play("Idle"); // Or whatever your default state is called.
    }
    private void HandleAttacks()
    {
        if (!attackQueued) return;

        if (isAttacking)
        {
            if (comboStep == 1)
            {
                PerformAttack(2);
            }
        }
        else
        {
            PerformAttack(1);
        }
        attackQueued = false;
    }

    private void PerformAttack(int step)
    {
        if (!isAttacking && step > 1) return;

        isAttacking = true;
        playerMovement.SetAttacking(true);

        if (step == 1)
        {
            animator.SetTrigger(attack1TriggerHash);
            comboStep = 1;
        }
        else if (step == 2)
        {
            animator.SetTrigger(attack2TriggerHash);
            comboStep = 2;
        }
    }
    public void OnMyAttackWasParried(EnemyAI parryingEnemy) // We now need to know WHO parried us.
    {
        consecutiveParries++;
        Debug.Log($"<color=orange>PLAYER was parried! Consecutive parries: {consecutiveParries}</color>");

        if (consecutiveParries >= parriesUntilGuardBreak)
        {
            Debug.Log($"<color=red>GUARD BREAK! Player parried {consecutiveParries} times.</color>");

            // 1. Trigger the player's own stun/knockback visuals.
            if (playerHealth != null)
            {
                playerHealth.TriggerGuardBreak();
            }

            // --- THIS IS THE GUARANTEED FIX ---
            // 2. COMMAND the enemy that parried us to start its counter-attack.
            if (parryingEnemy != null)
            {
                Debug.Log($"<color=red>Commanding {parryingEnemy.name} to counter-attack!</color>");
                parryingEnemy.ForceCounterAttack();
            }
            // --- END OF FIX ---

            // 3. Start the player's attack lockout coroutine.
            StartCoroutine(GuardBreakLockoutCoroutine());
        }
    }
    private IEnumerator GuardBreakLockoutCoroutine()
    {
        isAttackDisabled = true; // Disable attacking.
        consecutiveParries = 0;  // Reset the counter.

        yield return new WaitForSeconds(guardBreakAttackLockout);

        isAttackDisabled = false; // Re-enable attacking.
        Debug.Log("<color=green>Guard Break lockout finished. Player can attack again.</color>");
    }



    public void PerformLunge()
    {
        if (playerMovement == null) return;
        StartCoroutine(LungeCoroutine());
    }

    private IEnumerator LungeCoroutine()
    {
        float timer = 0f;
        Vector2 direction = playerMovement.IsFacingRight() ? Vector2.right : Vector2.left;

        while (timer < lungeDuration)
        {
            // Calculate the movement for this frame.
            Vector2 moveStep = direction * lungeSpeed * Time.deltaTime;
            // Apply the movement using MovePosition.
            rb.MovePosition(rb.position + moveStep);

            timer += Time.deltaTime;
            yield return null;
        }
    }

    public void FinishAttack()
    {
        isAttacking = false;
        playerMovement.SetAttacking(false);
        comboStep = 0;

        if (attackQueued)
        {
            HandleAttacks();
        }
    }

    public void TriggerSlashEffect1()
    {
        if (slashEffect1 != null) slashEffect1.Play();
    }

    public void TriggerSlashEffect2()
    {
        if (slashEffect2 != null) slashEffect2.Play();
    }
    void FixedUpdate()
    {
        // We check for damage in FixedUpdate for reliable physics detection.
        if (isDamageFrameActive)
        {
            CheckForEnemyDamage();
        }
    }

    private void CheckForEnemyDamage()
    {
        if (hasDealtDamageThisAttack) return;

        Collider2D[] enemiesHit = Physics2D.OverlapBoxAll(attackDamagePoint.position, attackDamageAreaSize, 0f, enemyLayer);

        foreach (Collider2D enemy in enemiesHit)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
              

                enemyHealth.TakeDamage(attackDamage, this);
                hasDealtDamageThisAttack = true;
            }
        }
    }
    public void ResetParryCounter()
    {
        Debug.Log("<color=green>Attack landed! Resetting consecutive parry counter.</color>");
        consecutiveParries = 0;
    }
    // --- NEW ANIMATION EVENT METHODS ---

    /// <summary>
    /// Called by an animation event to start the damage window for the player's attack.
    /// </summary>
    public void StartDamageDetection()
    {
        isDamageFrameActive = true;
        hasDealtDamageThisAttack = false; // Reset for the new attack.
    }

    /// <summary>
    /// Called by an animation event to end the damage window.
    /// </summary>
    public void StopDamageDetection()
    {
        isDamageFrameActive = false;
    }
    public void AlertEnemiesOfAttack()
    {
        float notificationRange = 10f;
        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position, notificationRange, enemyLayer);

        foreach (Collider2D enemy in nearbyEnemies)
        {
            EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                // --- THIS IS THE KEY CHANGE ---
                // We are no longer calling OnPlayerAttack().
                // We are calling a new, safer method: PrepareForPlayerAttack().
                enemyAI.PrepareForPlayerAttack();
            }
        }
    }

    public bool IsAttacking()
    {
        return isAttacking;
    }
    // Add this Gizmo method to visualize the player's attack range.
    private void OnDrawGizmosSelected()
    {
        if (attackDamagePoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(attackDamagePoint.position, attackDamageAreaSize);
        }
    }
}
