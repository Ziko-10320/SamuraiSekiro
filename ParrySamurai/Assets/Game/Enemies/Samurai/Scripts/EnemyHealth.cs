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

    [Tooltip("The UI object that appears over the enemy when they can be finished.")]
    [SerializeField] private GameObject finisherPromptUI;
    private bool isFinishable = false;

    private readonly int enterFinishableStateTriggerHash = Animator.StringToHash("enterFinishableState");

    [Header("Finisher Blood Effects")]
    [Tooltip("The particle system prefab for the head blood effect.")]
    [SerializeField] private ParticleSystem bloodHeadEffectPrefab; 
    [Tooltip("The transform where the head blood should spawn.")]
    [SerializeField] private Transform bloodHeadSpawnPoint;

    [Tooltip("The particle system prefab for the body blood effect.")]
    [SerializeField] private ParticleSystem bloodBodyEffectPrefab; 
    [Tooltip("The transform where the body blood should spawn.")]
    [SerializeField] private Transform bloodBodySpawnPoint;

    [Header("Finisher Blood Effects (Flipped)")]
    [Tooltip("The FLIPPED particle system prefab for the head blood effect.")]
    [SerializeField] private ParticleSystem bloodHeadEffectPrefab_Flipped; 
    [Tooltip("The FLIPPED transform where the head blood should spawn.")]
    [SerializeField] private Transform bloodHeadSpawnPoint_Flipped; 

    [Tooltip("The FLIPPED particle system prefab for the body blood effect.")]
    [SerializeField] private ParticleSystem bloodBodyEffectPrefab_Flipped; 
    [Tooltip("The FLIPPED transform where the body blood should spawn.")]
    [SerializeField] private Transform bloodBodySpawnPoint_Flipped;
    private bool isInCombo = false;
    private bool hasTakenDamageThisCombo = false;
    void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        followScript = GetComponent<EnemyFollow>(); // Get the follow script
        rb = GetComponent<Rigidbody2D>();
         enemyAI = GetComponent<EnemyAI>();
        if (finisherPromptUI != null)
        {
            finisherPromptUI.SetActive(false);
        }
    }
    public void ActivateComboArmor()
    {
        isInCombo = true;
        hasTakenDamageThisCombo = false; // Reset the shield at the start of every combo.
    }

    public void DeactivateComboArmor()
    {
        isInCombo = false;
    }

    public void StartParryState()
    {
        // This is called instantly by the AI. No more waiting for animations.
        isParrying = true;
    }

    public void TakeDamage(int damageAmount, AttackManager playerAttackManager = null)
    {
        if (isInCombo)
        {
            // 2. Check if it has already taken its one allowed hit for this combo.
            if (hasTakenDamageThisCombo)
            {
                Debug.Log("<color=grey>Enemy is in combo and has already taken damage. Ignoring hit.</color>");
                return; // Do nothing. The enemy is invincible for the rest of the combo.
            }
            else
            {
                // This is the FIRST hit during the combo.
                Debug.Log("<color=orange>Enemy took its one allowed hit during the combo.</color>");
                currentHealth -= damageAmount;
                hasTakenDamageThisCombo = true; // Flip the switch. No more damage allowed.

                // We do NOT play the "take damage" animation because it would interrupt the combo.
                // We just play the blood effect.
                if (bloodEffect != null && bloodEffectSpawnPoint != null)
                {
                    ParticleSystem newBloodEffect = Instantiate(bloodEffect, bloodEffectSpawnPoint.position, Quaternion.identity);
                    newBloodEffect.Play();
                }

                // Check for death, but don't play animations.
                if (currentHealth <= 0) Die();

                // IMPORTANT: We stop here. We do not want any other logic (like knockback) to run.
                return;
            }
        }
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
    public bool IsInUninterruptibleCombo()
    {
        return isInCombo;
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
        // This check is still good. It prevents the method from running multiple times.
        if (isFinishable)
        {
            return;
        }

        Debug.Log("<color=orange>Enemy health at 0. Entering FINISHABLE state.</color>");
        isFinishable = true; // The C# bool is still needed for the logic, but not for the Animator.

        // --- THIS IS THE GUARANTEED FIX ---
        // We are now using a Trigger. It fires once and cannot get stuck in a loop.
        animator.SetTrigger(enterFinishableStateTriggerHash);
        // --- END OF FIX ---

        // The rest of the lockdown code is correct.
        if (GetComponent<EnemyAI>() != null) GetComponent<EnemyAI>().enabled = false;
        if (GetComponent<EnemyFollow>() != null) GetComponent<EnemyFollow>().enabled = false;
        if (GetComponent<EnemyAttack>() != null) GetComponent<EnemyAttack>().enabled = false;

        animator.applyRootMotion = false;
        rb.isKinematic = true;
        rb.velocity = Vector2.zero;

        gameObject.layer = LayerMask.NameToLayer("Finishable");

        if (finisherPromptUI != null)
        {
            finisherPromptUI.SetActive(true);
        }
    }
    public void CameraShake()
    {
        CameraShakerHandler.Shake(CameraShakeParry);
    }
    public void TriggerBloodHeadEffect()
    {
        // We need to ask the Follow script which way the enemy is facing.
        if (followScript == null) return;

        // Check if the enemy is facing right.
        if (followScript.IsFacingRight())
        {
            // Use the normal, right-facing effects.
            if (bloodHeadEffectPrefab != null && bloodHeadSpawnPoint != null)
            {
                Instantiate(bloodHeadEffectPrefab, bloodHeadSpawnPoint.position, bloodHeadSpawnPoint.rotation);
            }
        }
        else // The enemy is facing left.
        {
            // Use the new, flipped effects.
            if (bloodHeadEffectPrefab_Flipped != null && bloodHeadSpawnPoint_Flipped != null)
            {
                Instantiate(bloodHeadEffectPrefab_Flipped, bloodHeadSpawnPoint_Flipped.position, bloodHeadSpawnPoint_Flipped.rotation);
            }
        }
    }


    /// <summary>
    /// This public method is called by an Animation Event during the ReceiveFinisher animation.
    /// </summary>
    public void TriggerBloodBodyEffect()
    {
        if (followScript == null) return;

        if (followScript.IsFacingRight())
        {
            // Use the normal, right-facing effects.
            if (bloodBodyEffectPrefab != null && bloodBodySpawnPoint != null)
            {
                Instantiate(bloodBodyEffectPrefab, bloodBodySpawnPoint.position, bloodBodySpawnPoint.rotation);
            }
        }
        else // The enemy is facing left.
        {
            // Use the new, flipped effects.
            if (bloodBodyEffectPrefab_Flipped != null && bloodBodySpawnPoint_Flipped != null)
            {
                Instantiate(bloodBodyEffectPrefab_Flipped, bloodBodySpawnPoint_Flipped.position, bloodBodySpawnPoint_Flipped.rotation);
            }
        }
    }
    public bool IsFinishable()
    {
        return isFinishable;
    }
    public void MarkAsFinished()
    {
        // This flips the switch, making it impossible to finish this enemy again.
        isFinishable = false;

        // It's also a good idea to hide the UI prompt immediately.
        if (finisherPromptUI != null)
        {
            finisherPromptUI.SetActive(false);
        }
    }

    /// <summary>
    /// This is called by the player to officially kill the enemy after the finisher animation.
    /// </summary>
    public void ExecuteDeath()
    {
        Debug.Log("<color=red>Enemy has been executed. Destroying GameObject.</color>");
        // You can add loot drops or XP gain here before destroying.
        Destroy(gameObject,3);
    }
}
