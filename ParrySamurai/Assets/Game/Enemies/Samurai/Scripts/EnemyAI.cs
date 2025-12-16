// EnemyAI.cs (FINAL - With Brain Lock & Trigger Reset)

using System.Collections;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Parry Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float parryChance = 0.5f;

    [Header("Components")]
    private Animator animator;
    private EnemyHealth healthScript;

    private readonly int parryTriggerHash = Animator.StringToHash("parry");
    private readonly int counterAttackTriggerHash = Animator.StringToHash("counterAttack");

   

    // --- THIS IS THE NEW "BRAIN LOCK" ---
    private bool isLocked = false;
    [Header("Counter Attack Settings")]
    [SerializeField] private EnemyAttack enemyAttackScript;
    [Tooltip("How long to wait after the warning before lunging.")]
    [SerializeField] private float counterWarningDelay = 0.6f;
    [SerializeField] private ParticleSystem counterWarningGlint;
    private bool isReadyToParry = false;

    [Header("AI Behavior Settings")]
    [Tooltip("If player is closer than this, dash away.")]
    [SerializeField] private float defensiveDashDistance = 1.5f;

    [Tooltip("If player is farther than this, throw a projectile.")]
    [SerializeField] private float rangedAttackDistance = 7f;

    [Tooltip("The cooldown for the ranged attack.")]
    [SerializeField] private float rangedAttackCooldown = 4f;
    private bool canThrow = true;

    // --- ADD THESE NEW ANIMATION HASHES ---
    private readonly int dashBackwardTriggerHash = Animator.StringToHash("dashBackward");
    private readonly int throwSlashTriggerHash = Animator.StringToHash("throwSlash");
    private EnemyAttack attackScript;
    private Rigidbody2D rb;
    private EnemyFollow followScript;
    [Header("Defensive Dash Settings")]
    [SerializeField] private float minDashCooldown = 5f;
    [SerializeField] private float maxDashCooldown = 10f;
    private float dashCooldownTimer = 0f;
    [Header("Ranged Attack")]
    [SerializeField] private GameObject slashProjectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    private bool isClashing = false;
    private Coroutine knockbackCoroutine;
    [Header("Parry Priority System")]
    [SerializeField] private int successfulParriesRequired = 3;
    private int currentParryCount = 0;
    [HideInInspector] public bool isPrioritizingParry = true; // Start with parry priority
    [Tooltip("Chance to attack even while prioritizing parry")]
    [Range(0f, 1f)]
    [SerializeField] public float passiveAttackChance = 0.3f;
    [Header("Front Kick Counter System")]
    [Tooltip("How many successful parries before triggering front kick")]
    [SerializeField] private int parriesBeforeFrontKick = 3; // You can set this to 3 or 4
    private int currentFrontKickCounter = 0;
    private readonly int frontKickTriggerHash = Animator.StringToHash("frontKick");
    [Header("Random Front Kick System")]
    [Tooltip("Chance to randomly trigger front kick when idle (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float randomFrontKickChance = 0.15f; // 15% chance
    [Tooltip("Cooldown between random front kicks")]
    [SerializeField] private float randomFrontKickCooldown = 8f;
    private float lastRandomFrontKickTime = -999f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        healthScript = GetComponent<EnemyHealth>();
        attackScript = GetComponent<EnemyAttack>();
        followScript = GetComponent<EnemyFollow>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (healthScript != null && (healthScript.isDead || healthScript.IsFinishable()))
        {
            return;
        }

        if (isLocked || isClashing || (healthScript != null && healthScript.IsStunned()))
        {
            return;
        }
        if (attackScript != null && !attackScript.CanAttack())
        {
            return;
        }
        if (isLocked || (healthScript != null && healthScript.IsStunned()))
        {
            return;
        }

        // Tick down the dash cooldown timer.
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        // Failsafe check
        if (attackScript == null || attackScript.GetPlayerTarget() == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, attackScript.GetPlayerTarget().position);

        // --- NEW: Random front kick chance when idle ---
        if (!isLocked && !attackScript.CanAttack() && Time.time - lastRandomFrontKickTime > randomFrontKickCooldown)
        {
            float roll = Random.Range(0f, 1f);
            if (roll <= randomFrontKickChance)
            {
                // Check if player is in range
                if (distanceToPlayer <= attackScript.GetAttackRange())
                {
                    Debug.Log("<color=magenta>RANDOM FRONT KICK TRIGGERED!</color>");
                    lastRandomFrontKickTime = Time.time;
                    StartCoroutine(FrontKickSequence());
                    return;
                }
            }
        }

        // --- THE NEW, SMARTER DECISION LOGIC ---

        // Decision 1: Player is too close AND cooldown is ready. Dash away.
        if (distanceToPlayer < defensiveDashDistance && dashCooldownTimer <= 0)
        {
            StartCoroutine(DefensiveDashSequence());
            return;
        }

        // Decision 2: Player is too far AND ranged attack is ready.
        if (distanceToPlayer > rangedAttackDistance && canThrow)
        {
            StartCoroutine(RangedAttackSequence());
            return;
        }

        // Decision 3: (Implicit) If neither of the above, the EnemyAttack script will handle melee.
    }
    public void SetClashState(bool state)
    {
        isClashing = state;
    }

    // --- MODIFY THE DefensiveDashSequence() COROUTINE TO RESET THE COOLDOWN ---
    private IEnumerator DefensiveDashSequence()
    {
        Debug.Log("<color=blue>AI: Player is too close! Commanding a dash.</color>");
        isLocked = true;
        animator.SetTrigger(dashBackwardTriggerHash);

        // COMMAND the follow script to perform the dash.
        if (followScript != null)
        {
            followScript.TriggerDefensiveDash();
        }

        // Wait for the dash to finish.
        yield return new WaitForSeconds(0.5f);

        isLocked = false;

        // --- THIS IS THE FIX ---
        // After dashing, set a new random cooldown.
        dashCooldownTimer = Random.Range(minDashCooldown, maxDashCooldown);
        Debug.Log($"AI: Dash cooldown set to {dashCooldownTimer} seconds.");
        // --- END OF FIX ---
    }
    private IEnumerator RangedAttackSequence()
    {
        Debug.Log("<color=purple>AI: Player is too far! Throwing projectile.</color>");
        isLocked = true;
        canThrow = false;

        // --- THIS IS THE GUARANTEED FIX ---
        // 1. TELL THE ATTACK SCRIPT TO SHUT DOWN ITS MELEE LOGIC.
        if (attackScript != null)
        {
            attackScript.SetRangedAttackState(true);
        }
        // --- END OF FIX ---

        animator.SetTrigger(throwSlashTriggerHash);

        // Wait for the animation to finish.
        yield return new WaitForSeconds(1.5f); // Adjust to your animation length

        // --- THIS IS THE GUARANTEED FIX ---
        // 2. TELL THE ATTACK SCRIPT TO RE-ENABLE ITS MELEE LOGIC.
        if (attackScript != null)
        {
            attackScript.SetRangedAttackState(false);
        }
        // --- END OF FIX ---

        isLocked = false;

        // Cooldown timer
        yield return new WaitForSeconds(rangedAttackCooldown);
        canThrow = true;
    }
    public void OnParryDecision()
    {
        if (!isReadyToParry) return;

        // In parry priority mode: ALWAYS PARRY (100%)
        if (isPrioritizingParry)
        {
            Debug.Log("<color=cyan>ENEMY AI: PARRYING! (Parry Priority Mode - 100% chance)</color>");
            if (healthScript != null)
            {
                healthScript.StartParryState();
            }
            animator.SetTrigger(parryTriggerHash);
        }
        else
        {
            // In combo mode: attack aggressively, rarely parry
            float roll = Random.Range(0f, 1f);
            if (roll <= 0.2f) // Only 20% chance to parry in combo mode
            {
                Debug.Log("<color=cyan>ENEMY AI: Decision is PARRY (Combo Priority Mode).</color>");
                if (healthScript != null)
                {
                    healthScript.StartParryState();
                }
                animator.SetTrigger(parryTriggerHash);
            }
            else
            {
                Debug.Log("<color=yellow>ENEMY AI: Attacking in combo mode.</color>");
                // Don't parry, let the attack script handle it
            }
        }

        isReadyToParry = false;
    }
    public void SpawnSlashProjectile()
    {
        if (slashProjectilePrefab == null || projectileSpawnPoint == null || attackScript.GetPlayerTarget() == null || followScript == null)
        {
            Debug.LogError("Cannot spawn projectile! A required component (Prefab, Spawn Point, Player Target, or Follow Script) is missing in EnemyAI!", this);
            return;
        }

        Debug.Log("<color=purple>SPAWNING PROJECTILE NOW from EnemyAI.</color>");

        // Get the original rotation from the prefab.
        Quaternion spawnRotation = slashProjectilePrefab.transform.rotation;

        // --- THIS IS THE GUARANTEED FIX ---
        // 1. ASK the Follow Script which way it is facing.
        if (!followScript.IsFacingRight())
        {
            // 2. If it's not facing right (i.e., it's facing left), then apply the 180-degree rotation.
            spawnRotation *= Quaternion.Euler(0, 180, 0);
            Debug.Log("Enemy is facing left. Applying 180-degree rotation to projectile.");
        }
        // --- END OF FIX ---

        // Instantiate the projectile using the correct rotation.
        GameObject projectileObj = Instantiate(slashProjectilePrefab, projectileSpawnPoint.position, spawnRotation);

        SlashProjectile projectile = projectileObj.GetComponent<SlashProjectile>();
        if (projectile != null)
        {
            Vector2 directionToPlayer = (attackScript.GetPlayerTarget().position - projectileSpawnPoint.position).normalized;
            projectile.Launch(directionToPlayer);
        }
        else
        {
            Debug.LogError("Spawned projectile, but it is missing the 'SlashProjectile' script!", projectileObj);
        }
    }
    public void ForceCounterAttack()
    {
        Debug.Log("<color=red>ENEMY AI: Received ForceCounterAttack command! Starting sequence.</color>");
        // We don't check for a counter here, we just start the coroutine.
        StartCoroutine(CounterAttackSequence());
    }

    private IEnumerator CounterAttackSequence()
    {
        isLocked = true; // Lock the AI during counter

        // --- The warning glint logic ---
        if (counterWarningGlint != null)
        {
            counterWarningGlint.gameObject.SetActive(true);
            counterWarningGlint.Play();
        }

        // Wait for the delay.
        yield return new WaitForSeconds(counterWarningDelay);

        // Fire the trigger for the actual counter-attack animation.
        Debug.Log("Warning finished. Firing 'counterAttack' trigger!");
        if (healthScript != null)
        {
            healthScript.StartCounterAttackState();
        }
        animator.SetTrigger(counterAttackTriggerHash);

        // Wait for counter-attack animation to finish
        yield return new WaitForSeconds(1.5f); // Adjust to match your counter animation length

        // --- The logic to disable the glint ---
        if (counterWarningGlint != null)
        {
            counterWarningGlint.gameObject.SetActive(false);
        }

        // CRITICAL: Reset the counter-attack state
        if (healthScript != null)
        {
            healthScript.EndCounterAttackState();
        }

        isLocked = false; // Unlock the AI so it can act again
        Debug.Log("<color=green>Counter-attack sequence complete. AI unlocked.</color>");
    }
    public void PrepareForPlayerAttack()
    {
        // This is called by the player's animation event.
        // It just flips a switch to say "I'm ready."

        if (healthScript != null && healthScript.IsStunned())
        {
            Debug.Log("<color=red>ENEMY AI: Aborting PrepareForAttack because I am stunned!</color>");
            return;
        }

        // In parry priority mode, ALWAYS be ready to parry
        if (isPrioritizingParry)
        {
            isReadyToParry = true;
            Debug.Log("<color=green>ENEMY AI: READY TO PARRY (Parry Priority Mode)!</color>");
            // Don't start a timer - keep ready until the attack comes
        }
        else
        {
            // In combo mode, use the normal readiness window
            isReadyToParry = true;
            StartCoroutine(ParryReadinessWindow());
        }
    }

    private IEnumerator ParryReadinessWindow()
    {
        // Wait for a short time (e.g., half a second). This is the window
        // during which the enemy is actively looking for a parry.
        yield return new WaitForSeconds(0.5f);

        // Only close the window if we're NOT in parry priority mode
        if (!isPrioritizingParry)
        {
            isReadyToParry = false;
        }
    }
    public void ApplyKnockback(Transform source, float distance, float duration)
    {
        // Failsafe: If a knockback is already running, stop it before starting a new one.
        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
        }
        // Start the coroutine ON THIS SCRIPT (the EnemyAI script).
        knockbackCoroutine = StartCoroutine(KnockbackCoroutine(source, distance, duration));
    }

    // This coroutine does the actual work. It should be private.
    private IEnumerator KnockbackCoroutine(Transform source, float distance, float duration)
    {
        isLocked = true;
        rb.velocity = Vector2.zero;

        // --- THIS IS THE GUARANTEED HORIZONTAL FIX ---

        // 1. Calculate the raw direction from the source (player) to us (enemy).
        Vector2 rawDirection = transform.position - source.position;

        // 2. Create a NEW, flattened direction vector. We ONLY take the 'x' value.
        //    The 'y' and 'z' are forced to be 0.
        Vector2 horizontalDirection = new Vector2(rawDirection.x, 0f);

        // 3. Normalize the new horizontal direction to get a clean unit vector.
        Vector2 knockbackDirection = horizontalDirection.normalized;

        // --- END OF FIX ---

        // The rest of the code now uses the guaranteed-horizontal 'knockbackDirection'.
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + (Vector3)knockbackDirection * distance;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            // Using SmoothStep for a better feel.
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / duration);
            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Final position set, just in case.
        transform.position = endPosition;
        isLocked = false;
    }
    public void OnSuccessfulParry()
    {
        // Increment the front kick counter
        currentFrontKickCounter++;
        Debug.Log($"<color=cyan>Parry #{currentFrontKickCounter} successful!</color>");

        // Check if we've reached the threshold for front kick
        if (currentFrontKickCounter >= parriesBeforeFrontKick)
        {
            Debug.Log("<color=orange>PARRY THRESHOLD REACHED! Checking if player is in range...</color>");

            // ONLY trigger front kick if player is in range
            if (attackScript != null && attackScript.GetPlayerTarget() != null)
            {
                float distanceToPlayer = Vector2.Distance(transform.position, attackScript.GetPlayerTarget().position);

                if (distanceToPlayer <= attackScript.GetAttackRange())
                {
                    Debug.Log("<color=orange>Player is in range! Triggering FRONT KICK!</color>");
                    StartCoroutine(FrontKickSequence());
                    currentFrontKickCounter = 0; // Reset counter
                    isPrioritizingParry = false; // Switch to combo mode
                    lastRandomFrontKickTime = Time.time; // Reset cooldown
                }
                else
                {
                    Debug.Log("<color=red>Player is OUT OF RANGE! Front kick cancelled!</color>");
                    currentFrontKickCounter = 0; // Reset counter anyway
                }
            }
        }
        // ADD THIS: Random chance to front kick even without reaching threshold
        else
        {
            float roll = Random.Range(0f, 1f);
            if (roll <= randomFrontKickChance * 0.5f) // Half chance compared to idle
            {
                if (attackScript != null && attackScript.GetPlayerTarget() != null)
                {
                    float distanceToPlayer = Vector2.Distance(transform.position, attackScript.GetPlayerTarget().position);
                    if (distanceToPlayer <= attackScript.GetAttackRange())
                    {
                        Debug.Log("<color=magenta>RANDOM FRONT KICK AFTER PARRY!</color>");
                        StartCoroutine(FrontKickSequence());
                        currentFrontKickCounter = 0;
                        isPrioritizingParry = false;
                        lastRandomFrontKickTime = Time.time;
                    }
                }
            }
        }
    }
    private IEnumerator FrontKickSequence()
    {
        isLocked = true;

        // Double-check player is still in range before kicking
        if (attackScript != null && attackScript.GetPlayerTarget() != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, attackScript.GetPlayerTarget().position);

            if (distanceToPlayer > attackScript.GetAttackRange())
            {
                Debug.Log("<color=red>Player moved out of range! Cancelling front kick!</color>");
                isLocked = false;
                yield break; // CHANGE: Use 'yield break' instead of 'return'
            }
        }

        // Play front kick animation
        animator.SetTrigger(frontKickTriggerHash);
        Debug.Log("<color=yellow>Playing FRONT KICK animation!</color>");

        // Wait for the kick animation to play
        yield return new WaitForSeconds(0.5f);

        isLocked = false;

        // NOW trigger the counter-attack
        Debug.Log("<color=red>Front kick complete! NOW triggering counter-attack!</color>");
        ForceCounterAttack();
    }
    public void OnComboFinished()
    {
        // Reset to parry priority after combo
        isPrioritizingParry = true;
        currentParryCount = 0;
        Debug.Log("<color=cyan>RESET TO PARRY PRIORITY MODE!</color>");
    }
}

