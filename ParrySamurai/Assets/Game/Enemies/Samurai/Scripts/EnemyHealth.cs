// EnemyHealth.cs

using FirstGearGames.SmoothCameraShaker;
using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    [Header("Parry & Stun Settings")]
    [Tooltip("How long the enemy is stunned after being parried.")]
    [SerializeField] private float parryStunDuration = 1.5f;

    [Header("Components")]
    private Animator animator;
    private EnemyFollow followScript; // To disable movement when stunned

    // --- State Control ---
    private bool isStunned = false;
    [SerializeField] private float parryKnockbackForce = 8f;
    [Tooltip("How long the knockback force is applied.")]
    [SerializeField] private float parryKnockbackDuration = 0.15f;
    private Rigidbody2D rb;

    // --- Animation Hashes ---
    private readonly int getParriedTriggerHash = Animator.StringToHash("getParried");
    private readonly int takeDamageTriggerHash = Animator.StringToHash("takeDamage");
    private readonly int deathTriggerHash = Animator.StringToHash("death");
    private bool isParrying = false;
    public ShakeData CameraShakeParry;

    [Tooltip("The particle effect to spawn when a parry is successful.")]
    [SerializeField] private ParticleSystem parrySparksEffect;
    [Tooltip("The spawn point for the parry sparks.")]
    [SerializeField] private Transform parrySparksSpawnPoint;
    private EnemyAI enemyAI;
    private bool isAttacking = false;
    [Header("Counter-Attack Settings")]
    [SerializeField] private ParticleSystem counterWarningEffect;
    [Header("Damage Effects")]
    [Tooltip("The blood particle effect to play when taking damage.")]
    [SerializeField] private ParticleSystem bloodEffect;
    [Tooltip("The transform where the blood effect should spawn.")]
    [SerializeField] private Transform bloodEffectSpawnPoint;
    [Tooltip("How far the enemy gets knocked back when hit.")]
    [SerializeField] private float knockbackDistance = 0.5f;
    [Tooltip("How quickly the knockback happens (in seconds).")]
    [SerializeField] private float knockbackDuration = 0.1f;
    void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        followScript = GetComponent<EnemyFollow>(); // Get the follow script
        rb = GetComponent<Rigidbody2D>();
         enemyAI = GetComponent<EnemyAI>();
    }

   
    public void StartParryState()
    {
        // This is called instantly by the AI. No more waiting for animations.
        isParrying = true;
    }

    public void TakeDamage(int damageAmount, AttackManager playerAttackManager = null)
    {
        // --- PARRY LOGIC (This only runs if we are NOT stunned) ---
        if (!isStunned)
        {
            // Ask the AI to make a parry decision.
            if (enemyAI != null)
            {
                enemyAI.OnParryDecision();
            }

            // Check if the decision resulted in a successful parry.
            if (isParrying)
            {
                Debug.Log("<color=cyan>PARRY SUCCESS!</color>");
                // ... (All your parry success logic is perfect) ...
                CameraShakerHandler.Shake(CameraShakeParry);
                if (parrySparksEffect != null) { Instantiate(parrySparksEffect, parrySparksSpawnPoint.position, Quaternion.identity); }
                if (playerAttackManager != null && enemyAI != null) { playerAttackManager.OnMyAttackWasParried(enemyAI); }
                ZreyMovements playerMovement = FindObjectOfType<ZreyMovements>();
                if (playerMovement != null) { playerMovement.GetParried(this.transform); }

                // If we parry, we stop everything. Do not take damage.
                return;
            }
        }

        // --- THIS IS THE GUARANTEED FIX ---
        // If the code reaches here, it means one of two things:
        // 1. The enemy was stunned.
        // 2. The enemy was not stunned, but it failed its parry attempt.
        // In BOTH cases, the enemy MUST take damage.

        Debug.Log("<color=red>DAMAGE PHASE: Enemy is taking damage.</color>");
        currentHealth -= damageAmount;

        // Play blood effect.
        if (bloodEffect != null && bloodEffectSpawnPoint != null)
        {
            ParticleSystem newBloodEffect = Instantiate(bloodEffect, bloodEffectSpawnPoint.position, Quaternion.identity);
            newBloodEffect.Play();
        }

        // Reset the player's parry counter since they landed a clean hit.
        if (playerAttackManager != null)
        {
            playerAttackManager.ResetParryCounter();
        }

        // Check for death.
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Only play the "take damage" animation if not already in the "get parried" stun animation.
            // This prevents the animations from fighting.
           
                animator.SetTrigger(takeDamageTriggerHash);
            
        }
        // --- END OF FIX ---
    }
    private IEnumerator KnockbackCoroutine(Transform source, float distance, float duration)
    {
       
        rb.velocity = Vector2.zero;
        Vector2 knockbackDirection = (transform.position - source.position).normalized;
        Vector2 startPosition = transform.position;
        Vector2 endPosition = startPosition + knockbackDirection * distance;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            // Use MovePosition for physics-safe movement
            rb.MovePosition(Vector2.Lerp(startPosition, endPosition, elapsedTime / duration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// This is the public method the PlayerHealth script will call on a successful parry.
    /// </summary>
    public void GetParried(Transform parryingPlayer) // We now accept the player's transform
    {
        // We need the player's transform to calculate the knockback direction reliably.
        if (parryingPlayer == null) return;

        StartCoroutine(StunCoroutine(parryingPlayer));
    }

    private IEnumerator StunCoroutine(Transform parryingPlayer)
    {
        Debug.Log("<color=red>--- STUN COROUTINE STARTED ---</color>");

        // --- THIS IS THE GUARANTEED FIX ---
        // 1. SET THE STUN STATE. This is now the master state.
        isStunned = true;

        // 2. FORCE THE PARRY STATE TO BE FALSE.
        //    An enemy cannot be parrying while it is stunned. This kills the zombie parry.
        isParrying = false;
        // --- END OF FIX ---

        // 3. Disable movement and play the animation.
        if (followScript != null) followScript.enabled = false;
        animator.SetTrigger(getParriedTriggerHash);

      

        // ... (your knockback logic is perfect) ...
        rb.velocity = Vector2.zero;
        // ...

        // Wait for the stun duration.
        float stunTimer = parryStunDuration;
        while (stunTimer > 0)
        {
            // We add this check inside the loop to be extra safe.
            // If something else tries to make the enemy parry, we force it back to stunned.
            isStunned = true;
            isParrying = false;
            stunTimer -= Time.deltaTime;
            yield return null;
        }

        // --- Clean up after the stun is over ---
        Debug.Log("<color=green>--- STUN COROUTINE FINISHED ---</color>");
        isStunned = false;
        if (followScript != null) followScript.enabled = true;
    }
    public bool IsStunned()
    {
        return isStunned;
    }
   
    public void StartAttackState()
    {
        isAttacking = true;
    }

    public void EndAttackState()
    {
        isAttacking = false;
    }

    // Add a public method for the AI to check this state:
    public bool IsAttacking()
    {
        return isAttacking;
    }
    public bool IsParrying()
    {
        return isParrying;
    }

    /// <summary>
    /// This public method is called by an Animation Event at the END of the active parry frames.
    /// It tells the script that the parry window is now closed.
    /// </summary>
    public void StartParryWindow()
    {
        isParrying = true;
        // --- ADD THIS LOG ---
        Debug.Log("<color=lightblue>--- ENEMY HEALTH: Parry Window OPEN (isParrying = true). Animation event SUCCESS. ---</color>", this.gameObject);
    }

    public void EndParryWindow()
    {
        isParrying = false;
        // --- ADD THIS LOG ---
        Debug.Log("<color=grey>--- ENEMY HEALTH: Parry Window CLOSED (isParrying = false). Animation event SUCCESS. ---</color>", this.gameObject);
    }
   
    private void Die()
    {
        Debug.Log("Enemy has died!");
        animator.SetTrigger(deathTriggerHash);

        // Disable all enemy components to stop it from acting.
        GetComponent<EnemyFollow>().enabled = false;
        GetComponent<EnemyAttack>().enabled = false;
        gameObject.SetActive(false); // So it can't be hit anymore
        this.enabled = false;
    }
}
