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
        if (healthScript != null && healthScript.isDead)
        {
            // If dead, do ABSOLUTELY NOTHING. Stop the entire AI loop.
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
        // The brain does not think if it's locked or stunned.
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

        // --- THE NEW, SMARTER DECISION LOGIC ---

        // Decision 1: Player is too close AND cooldown is ready. Dash away.
        // This is the HIGHEST priority decision.
        if (distanceToPlayer < defensiveDashDistance && dashCooldownTimer <= 0)
        {
            StartCoroutine(DefensiveDashSequence());
            return; // IMPORTANT: If we decide to dash, we don't evaluate any other actions this frame.
        }

        // Decision 2: Player is too far AND ranged attack is ready.
        if (distanceToPlayer > rangedAttackDistance && canThrow)
        {
            StartCoroutine(RangedAttackSequence());
            return; // Stop here to not conflict with melee.
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

        float roll = Random.Range(0f, 1f);
        if (roll <= parryChance)
        {
            // --- THIS IS THE GUARANTEED FIX ---
            // 1. DECISION: PARRY!
            Debug.Log("<color=cyan>ENEMY AI: Decision is PARRY. Forcing state now.</color>");

            // 2. IMMEDIATELY tell the health script to enter the parry state.
            if (healthScript != null)
            {
                healthScript.StartParryState(); // We will create this new method.
            }

            // 3. Trigger the animation.
            animator.SetTrigger(parryTriggerHash);
            // --- END OF FIX ---
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
        // --- The warning glint logic is perfect ---
        if (counterWarningGlint != null)
        {
            counterWarningGlint.gameObject.SetActive(true);
            counterWarningGlint.Play();
        }

        // Wait for the delay.
        yield return new WaitForSeconds(counterWarningDelay);

        // Fire the trigger for the actual counter-attack animation.
        Debug.Log("Warning finished. Firing 'counterAttack' trigger!");
        animator.SetTrigger(counterAttackTriggerHash);

        // --- The logic to disable the glint is also perfect ---
        if (counterWarningGlint != null)
        {
            yield return new WaitForSeconds(1f);
            counterWarningGlint.gameObject.SetActive(false);
        }
    }
    public void PrepareForPlayerAttack()
    {
        // This is called by the player's animation event.
        // It just flips a switch to say "I'm ready."
        isReadyToParry = true;
        if (healthScript != null && healthScript.IsStunned())
        {
            Debug.Log("<color=red>ENEMY AI: Aborting PrepareForAttack because I am stunned!</color>");
            return;
        }
        // Start a coroutine to automatically turn the switch off after a moment.
        // This prevents the enemy from being "ready" forever.
        StartCoroutine(ParryReadinessWindow());
    }

    private IEnumerator ParryReadinessWindow()
    {
        // Wait for a short time (e.g., half a second). This is the window
        // during which the enemy is actively looking for a parry.
        yield return new WaitForSeconds(0.5f);
        isReadyToParry = false;
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
}

