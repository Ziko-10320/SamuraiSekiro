// PlayerHealth.cs (FINAL - Combined Health & Block System)
using FirstGearGames.SmoothCameraShaker;
using System.Collections;
using UnityEngine;
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    [Header("Blocking Settings")]
    [Tooltip("The Animator component on the player.")]
    [SerializeField] private Animator animator;

    // --- State Control ---
    public bool isBlocking = false;

    // --- Animation Hashes ---
    private readonly int isBlockingHash = Animator.StringToHash("isBlocking");
    private readonly int takeDamageTriggerHash = Animator.StringToHash("takeDamage"); // Optional: for a hurt animation
    private readonly int deathTriggerHash = Animator.StringToHash("death"); // Optional: for a death animation
    private readonly int parryTriggerHash = Animator.StringToHash("parry");
    private readonly int getParriedTriggerHash = Animator.StringToHash("GetParried");
    // --- Public property to let other scripts read the health ---
    public int CurrentHealth => currentHealth;

    [Header("Parry & Block Settings")]
    [Tooltip("How much damage is blocked when holding a normal block (e.g., 0.5 = 50% damage reduction).")]
    [Range(0f, 1f)]
    [SerializeField] private float blockDamageReduction = 0.5f;

    [Tooltip("The time window (in seconds) after starting a block where a parry is possible.")]
    [SerializeField] private float parryWindow = 0.3f;

    [Tooltip("The particle effect to spawn when a parry is successful.")]
    [SerializeField] private ParticleSystem parrySparksEffect;
    [Tooltip("The spawn point for the parry sparks.")]
    [SerializeField] private Transform parrySparksSpawnPoint;

    [Header("Time Control")]
    [Tooltip("How much to slow down time on a successful parry (e.g., 0.1 = 10% speed).")]
    [SerializeField] private float parryTimeScale = 0.1f;
    [Tooltip("How long the slow-motion effect lasts (in seconds).")]
    [SerializeField] private float parrySlowMoDuration = 0.2f;
    [Tooltip("The particle effect to spawn when a normal block is successful.")]
    [SerializeField]private ParticleSystem blockSparksEffect;
    [Tooltip("The spawn point for the normal block sparks.")]
    [SerializeField] private Transform blockSparksSpawnPoint;
// --- State Control ---
    private bool isParryWindowActive = false;
    private Coroutine parryWindowCoroutine;
    public ShakeData CameraShakeParry;
    [SerializeField] private ZreyMovements playerMovement;
    private AttackManager attackManager;
    [Header("Damage Effects")]
    [Tooltip("The blood particle effect to play when taking damage.")]
    [SerializeField] private ParticleSystem bloodEffect;
    [Tooltip("The transform where the blood effect should spawn.")]
    [SerializeField] private Transform bloodEffectSpawnPoint;
    [Tooltip("How far the player gets knocked back when hit.")]
    [SerializeField] private float knockbackDistance = 1f;
    [Tooltip("How quickly the knockback happens (in seconds).")]
    [SerializeField] private float knockbackDuration = 0.15f;
    [SerializeField] private float guardBreakKnockbackDistance = 3f;
    [SerializeField] private float guardBreakKnockbackDuration = 0.3f;
    void Awake()
    {
       
        // Start the game with full health.
        currentHealth = maxHealth;

        // Automatically find the Animator if you forget to assign it.
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (playerMovement == null)
        {
            playerMovement = GetComponent<ZreyMovements>();
        }
        attackManager = GetComponent<AttackManager>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            // ...and they are not already blocking...
            if (!isBlocking)
            {
                // 1. Check if they are currently attacking.
                if (attackManager != null && attackManager.IsAttacking())
                {
                    // 2. If yes, CANCEL the attack immediately.
                    attackManager.CancelAttack();
                }

                // 3. NOW, start blocking.
                StartBlocking();
            }
        }
        // Check if we should STOP blocking.
        else if (Input.GetMouseButtonUp(1) && isBlocking)
        {
            StopBlocking();
        }
    }

    private void StartBlocking()
    {
       
        isBlocking = true;
        animator.SetBool(isBlockingHash, true);
        Debug.Log("Player started blocking.");

        // Start the parry window!
        if (parryWindowCoroutine != null)
        {
            StopCoroutine(parryWindowCoroutine);
        }
        parryWindowCoroutine = StartCoroutine(ParryWindowCoroutine());
    }

    // In PlayerHealth.cs, replace your StopBlocking() method:

    private void StopBlocking()
    {
      
        isBlocking = false;
        animator.SetBool(isBlockingHash, false);
        Debug.Log("Player stopped blocking.");

        // If we stop blocking, the parry window must also close.
        if (parryWindowCoroutine != null)
        {
            StopCoroutine(parryWindowCoroutine);
            isParryWindowActive = false;
        }
    }
    /// <summary>
    /// This is the public method that enemies will call to deal damage.
    /// </summary>
    public void TakeDamage(int damageAmount, EnemyHealth attackingEnemy = null) // Added optional enemy parameter
    {
        // --- PARRY LOGIC ---
        // If the player is in the parry window...
        if (isParryWindowActive)
        {
            CameraShakerHandler.Shake(CameraShakeParry);
            Debug.Log("PARRY SUCCESSFUL!");
            if (attackingEnemy != null)
            {
                // If we do, COMMAND them to get stunned.
                Debug.Log($"<color=lime>COMMANDING enemy ({attackingEnemy.name}) to get stunned!</color>");
                attackingEnemy.GetParried(this.transform);
            }
            else
            {
                // If we don't know who attacked us (e.g., a projectile), we can't stun them.
                // This is a failsafe log.
                Debug.LogWarning("Player parried, but the source of the attack is unknown. Cannot stun enemy.");
            }
            animator.SetTrigger(parryTriggerHash);
           
            // 1. Don't take any damage.
            // 2. Play the parry sparks effect.
            if (parrySparksEffect != null && parrySparksSpawnPoint != null)
            {
                Instantiate(parrySparksEffect, parrySparksSpawnPoint.position, parrySparksSpawnPoint.rotation);
            }
          
            
            // 4. Trigger slow motion.
            StartCoroutine(SlowMoEffect());
            // 5. Stop the function here.
            return;
        }
        if (attackManager != null)
        {
            attackManager.CancelAttack();
        }
        if (bloodEffect != null && bloodEffectSpawnPoint != null)
        {
            // --- THIS IS THE FINAL, GUARANTEED FIX ---
            // 1. Instantiate the prefab and store the new copy in a variable.
            ParticleSystem newBloodEffect = Instantiate(bloodEffect, bloodEffectSpawnPoint.position, Quaternion.identity);

            // 2. Tell the NEW copy to play.
            newBloodEffect.Play();
            // --- END OF FINAL, GUARANTEED FIX ---
        }
        // --- Trigger Knockback ---
        // We only trigger knockback if an enemy is passed in.
        if (attackingEnemy != null && playerMovement != null)
        {
            // We can reuse the GetParried knockback from ZreyMovements, but we need to make it public.
            // Or, even better, let's make a new, more generic one.
            playerMovement.ApplyKnockback(attackingEnemy.transform, knockbackDistance, knockbackDuration);
        }
        // --- BLOCK LOGIC ---
        // If the player is blocking (but not parrying)...
        if (isBlocking)
        {
            CameraShakerHandler.Shake(CameraShakeParry);
            if (blockSparksEffect != null && blockSparksSpawnPoint != null)
            {
                Instantiate(blockSparksEffect, blockSparksSpawnPoint.position, blockSparksSpawnPoint.rotation);
            }
            // Calculate the reduced damage.
            int reducedDamage = Mathf.RoundToInt(damageAmount * (1 - blockDamageReduction));
            currentHealth -= reducedDamage;
            Debug.Log($"Attack BLOCKED! Player took {reducedDamage} reduced damage.");
            if (attackingEnemy != null && playerMovement != null)
            {
                playerMovement.GetParried(attackingEnemy.transform);
            }
        }
       
        // --- NORMAL DAMAGE LOGIC ---
        else
        {
            currentHealth -= damageAmount;
            Debug.Log($"Player took {damageAmount} damage. Current Health: {currentHealth}");
            animator.SetTrigger(takeDamageTriggerHash);
        }

        // Check for death (existing code is fine).
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    // Add these NEW coroutines to PlayerHealth.cs:

    private IEnumerator ParryWindowCoroutine()
    {
        isParryWindowActive = true;
        Debug.Log("Parry window OPEN.");
        yield return new WaitForSeconds(parryWindow);
        isParryWindowActive = false;
        Debug.Log("Parry window CLOSED.");
    }

    private IEnumerator SlowMoEffect()
    {
        Time.timeScale = parryTimeScale;
        // We use unscaledDeltaTime because the game time is now slowed down.
        yield return new WaitForSecondsRealtime(parrySlowMoDuration);
        Time.timeScale = 1f; // Return time to normal.
    }

    private void Die()
    {
        Debug.Log("Player has died!");
        animator.SetTrigger(deathTriggerHash);

        // Disable all player control scripts.
        GetComponent<ZreyMovements>().enabled = false;
        GetComponent<AttackManager>().enabled = false;
        this.enabled = false; // Disable this script as well.
    }
    public void TriggerGuardBreak()
    {
        animator.SetTrigger(getParriedTriggerHash);

        EnemyAI closestEnemy = FindClosestEnemy();
        if (closestEnemy != null && playerMovement != null)
        {
            // --- THIS IS THE FIX ---
            // Call the new, dedicated, smooth knockback method.
            playerMovement.TriggerGuardBreakKnockback(closestEnemy.transform, guardBreakKnockbackDistance, guardBreakKnockbackDuration);
        }
    }

    // --- Add this helper method to find the enemy ---
    private EnemyAI FindClosestEnemy()
    {
        EnemyAI[] enemies = FindObjectsOfType<EnemyAI>();
        EnemyAI closest = null;
        float minDistance = Mathf.Infinity;
        Vector3 currentPos = transform.position;
        foreach (EnemyAI enemy in enemies)
        {
            float distance = Vector3.Distance(enemy.transform.position, currentPos);
            if (distance < minDistance)
            {
                closest = enemy;
                minDistance = distance;
            }
        }
        return closest;
    }
    // --- Public method for other scripts to check the block status ---
    public bool IsCurrentlyBlocking()
    {
        return isBlocking;
    }
}
