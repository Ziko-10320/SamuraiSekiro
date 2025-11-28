// EnemyAttack.cs

using UnityEngine;
using System.Collections;

public class EnemyAttack : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("The player's transform that the enemy will attack.")]
    [SerializeField] private Transform playerTarget;

    [Header("Attack Settings")]
    [Tooltip("The distance from the origin point at which the enemy will start its attack.")]
    [SerializeField] private float attackRange = 2.5f;
    [Tooltip("The cooldown time between attacks (in seconds).")]
    [SerializeField] private float attackCooldown = 2f;
    [Tooltip("An empty GameObject used as the reference point for measuring the attack range.")]
    [SerializeField] private Transform attackRangeOrigin;

    [Header("Effects")]
    [Tooltip("The Particle System for the main attack slash effect.")]
    [SerializeField] private ParticleSystem slashEffect;
    [Tooltip("The Particle System for the warning/telegraph glint effect.")]
    [SerializeField] private ParticleSystem warningGlintEffect;

    [Header("Components")]
    private Animator animator;

    // --- State Control ---
    private bool canAttack = true;

    // --- Animation Hashes ---
    private readonly int attackTriggerHash = Animator.StringToHash("attack");

    [Header("Damage Settings")]
    [Tooltip("The amount of damage this attack deals.")]
    [SerializeField] private int attackDamage = 20;
    [Tooltip("An empty GameObject marking the center of the damage area.")]
    [SerializeField] private Transform damagePoint;
    [Tooltip("The size of the damage area (Width, Height).")]
    [SerializeField] private Vector2 damageAreaSize = new Vector2(1.5f, 1f);
    [Tooltip("The layer the player is on, so we know who to damage.")]
    [SerializeField] private LayerMask playerLayer;

// --- State Control ---
    private bool isDamageFrameActive = false;
    private bool hasDealtDamageThisAttack = false;
    private EnemyHealth healthScript;
  
    private bool isPerformingRangedAttack = false;
    void Awake()
    {
        animator = GetComponent<Animator>();
        healthScript = GetComponent<EnemyHealth>();
        // Failsafe: If no origin is set, use the enemy's own transform.
        if (attackRangeOrigin == null)
        {
            attackRangeOrigin = this.transform;
        }

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
                Debug.LogError("EnemyAttack: Player target not found! Please assign the player or tag them as 'Player'.", this);
                enabled = false; // Disable the script if there's no player.
            }
        }
    }

    void Update()
    {

        if (isPerformingRangedAttack)
        {
            return;
        }
        if (playerTarget == null || !canAttack || isPerformingRangedAttack || (healthScript != null && (healthScript.IsStunned() || healthScript.IsParrying() || healthScript.IsAttacking())))
        {
            return;
        }

        // Calculate the distance from our attack origin to the player.
        float distanceToPlayer = Vector2.Distance(attackRangeOrigin.position, playerTarget.position);

        // If the player is in range, perform the attack.
        if (distanceToPlayer <= attackRange)
        {
            PerformAttack();
        }
    }
    void FixedUpdate()
    {
        if (isDamageFrameActive && !isPerformingRangedAttack)
        {
            CheckForPlayerDamage();
        }
    }
    public void SetRangedAttackState(bool isRanged)
    {
        isPerformingRangedAttack = isRanged;
    }
    private void CheckForPlayerDamage()
    {
        if (isPerformingRangedAttack)
        {
            return;
        }
        if (hasDealtDamageThisAttack) return;

        Collider2D playerHit = Physics2D.OverlapBox(damagePoint.position, damageAreaSize, 0f, playerLayer);

        if (playerHit != null)
        {
            PlayerHealth playerHealth = playerHit.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                // --- THIS IS THE FINAL FIX ---
                // 1. Get the EnemyHealth component from this same GameObject.
                EnemyHealth myHealthScript = GetComponent<EnemyHealth>();

                // 2. Pass that specific component to the TakeDamage method.
                playerHealth.TakeDamage(attackDamage, myHealthScript);
                // --- END OF FIX ---

                hasDealtDamageThisAttack = true;
            }
        }
    }

    // --- NEW ANIMATION EVENT METHODS ---

    /// <summary>
    /// Called by an animation event to start the damage window.
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

  
    private void PerformAttack()
    {
        // We can't attack if we are on cooldown.
        if (!canAttack) return;

        // 1. Set the cooldown flag.
        canAttack = false;

        // 2. Trigger the animation.
        animator.SetTrigger(attackTriggerHash);

        // 3. Start the cooldown timer.
        StartCoroutine(AttackCooldownCoroutine());
    }

    private IEnumerator AttackCooldownCoroutine()
    {
        // Wait for the specified cooldown duration.
        yield return new WaitForSeconds(attackCooldown);

        // After the cooldown, the enemy can attack again.
        canAttack = true;
    }

    // --- ANIMATION EVENT METHODS ---

    /// <summary>
    /// This public method is called by an Animation Event to play the warning glint.
    /// </summary>
    public void TriggerWarningEffect()
    {
        if (warningGlintEffect != null)
        {
            warningGlintEffect.Play();
        }
        else
        {
            Debug.LogWarning("Warning Glint Effect is not assigned in the EnemyAttack script!", this);
        }
    }

    /// <summary>
    /// This public method is called by an Animation Event to play the main slash effect.
    /// </summary>
    public void TriggerAttackEffect()
    {
        if (slashEffect != null)
        {
            slashEffect.Play();
        }
        else
        {
            Debug.LogWarning("Slash Effect is not assigned in the EnemyAttack script!", this);
        }
    }
   

    // --- We also need to expose the player target and attack status for the AI ---
    public Transform GetPlayerTarget() 
    {
        return playerTarget;
    }
    public bool CanAttack() 
    { 
        return canAttack;
    }
    // --- GIZMO FOR VISUALIZATION ---
    private void OnDrawGizmosSelected()
    {
        // Draw the attack range circle (existing code)
        Vector3 origin = (attackRangeOrigin != null) ? attackRangeOrigin.position : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, attackRange);

        // Draw the damage area box
        if (damagePoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(damagePoint.position, damageAreaSize);
        }
    }
}
