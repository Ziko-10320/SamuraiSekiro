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
    private readonly int parry2TriggerHash = Animator.StringToHash("parry2"); // ADD THIS LINE
    private bool useAlternateParry = false;
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
    private readonly int heavyTakeDamageTriggerHash = Animator.StringToHash("HeavyTakeDamage");
    private bool isDodgeWindowActive = false;
    private string currentEnemyAttackType = "";
    private bool isDodgeActive = false;
    [Header("Sound Effects")]
    [SerializeField] private AudioSource audioSource;  // Audio source for playing sounds
    [SerializeField] private AudioClip[] parrySounds = new AudioClip[3];  // 3 parry sounds
    [SerializeField] private AudioClip blockHitSound;  // Sound when blocking and getting hit
    [SerializeField] private AudioClip[] enemyParrySounds = new AudioClip[2];  // 2 enemy parry sounds
    [SerializeField] private AudioClip[] playerHitSounds = new AudioClip[3];
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
        if (attackManager != null && attackManager.IsPerformingFinisher())
        {
            // If yes, ignore all input in this script.
            return;
        }
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
    public void TakeDamage(int damageAmount, EnemyHealth attackingEnemy = null, SlashProjectile sourceProjectile = null)
    {
        // NEW: Check if player is currently dodging (immune)
        if (isDodgeActive)
        {
            Debug.Log("<color=green>DODGE IMMUNITY! Damage blocked!</color>");
            return; // Exit immediately, take NO damage
        }

        if (attackManager != null && attackManager.IsPerformingFinisher())
        {
            // If yes, do nothing. The player is invincible.
            return;
        }
        // --- PARRY LOGIC ---
        // If the player is in the parry window...
        if (isParryWindowActive)
        {
            CameraShakerHandler.Shake(CameraShakeParry);
            Debug.Log("PARRY SUCCESSFUL!");
            PlayRandomSound(parrySounds);

            if (attackingEnemy != null)
            {
                // 2. ASK the enemy if it is in its special combo.
                if (attackingEnemy.IsInUninterruptibleCombo())
                {
                    // 3. If YES, do NOT stun it. Just log that we deflected the attack.
                    Debug.Log("<color=orange>Parried an uninterruptible combo attack! Enemy was not stunned.</color>");
                }
                else
                {
                    // 4. If NO, then it was a normal attack. Stun the enemy as usual.
                    Debug.Log($"<color=lime>Parried a normal attack! COMMANDING enemy ({attackingEnemy.name}) to get stunned!</color>");
                    attackingEnemy.GetParried(this.transform);
                }
            }
            if (sourceProjectile != null)
            {
                // If yes, tell the projectile that it has been parried.
                sourceProjectile.OnParried();
            }
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

            // ALTERNATE BETWEEN TWO PARRY ANIMATIONS
            if (useAlternateParry)
            {
                animator.SetTrigger(parry2TriggerHash);
                Debug.Log("<color=cyan>Playing PARRY 2 animation!</color>");
            }
            else
            {
                animator.SetTrigger(parryTriggerHash);
                Debug.Log("<color=cyan>Playing PARRY 1 animation!</color>");
            }
            useAlternateParry = !useAlternateParry; // Toggle for next parry

            // 1. Don't take any damage.
            // 2. Play the parry sparks effect.
            if (parrySparksEffect != null && parrySparksSpawnPoint != null)
            {
                Instantiate(parrySparksEffect, parrySparksSpawnPoint.position, parrySparksSpawnPoint.rotation);
            }

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
      
        // --- BLOCK LOGIC ---
        // If the player is blocking (but not parrying)...
        if (isBlocking)
        {
            CameraShakerHandler.Shake(CameraShakeParry);
            PlaySound(blockHitSound);

            if (blockSparksEffect != null && blockSparksSpawnPoint != null)
            {
                Instantiate(blockSparksEffect, blockSparksSpawnPoint.position, blockSparksSpawnPoint.rotation);
            }
            // Calculate the reduced damage.
            int reducedDamage = Mathf.RoundToInt(damageAmount * (1 - blockDamageReduction));
            currentHealth -= reducedDamage;
            Debug.Log($"Attack BLOCKED! Player took {reducedDamage} reduced damage.");
           
        }
       
        // --- NORMAL DAMAGE LOGIC ---
        else
        {
            currentHealth -= damageAmount;
            PlayRandomSound(playerHitSounds);
            Debug.Log($"Player took {damageAmount} damage. Current Health: {currentHealth}");
            if (attackingEnemy != null && playerMovement != null)
            {
                playerMovement.ApplyKnockback(attackingEnemy.transform, 1.5f, 0.2f);
            }

            animator.SetTrigger(takeDamageTriggerHash);
        }

        // Check for death (existing code is fine).
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }
    private IEnumerator HeavyKnockbackCoroutine(Transform damageSource, float knockbackDist, float knockbackDur, float delay)
    {
        // 1. Wait for the specified delay. This creates the "impact" moment.
        yield return new WaitForSeconds(delay);

        // 2. After the delay, apply the knockback.
        // This code is the same as before, it just runs later now.
        if (playerMovement != null)
        {
            playerMovement.ApplyKnockback(damageSource, knockbackDist, knockbackDur);
        }
    }
    public void TakeHeavyDamage(int damageAmount, Transform damageSource, float knockbackDist, float knockbackDur, int animationTriggerHash)
    {
        if (isParryWindowActive)
        {
            // PARRIED! Just apply small knockback to both
            Debug.Log("<color=cyan>PLAYER PARRIED THE HEAVY ATTACK! Both get knockback.</color>");

            CameraShakerHandler.Shake(CameraShakeParry);
            animator.SetTrigger(parryTriggerHash);

            if (parrySparksEffect != null && parrySparksSpawnPoint != null)
            {
                Instantiate(parrySparksEffect, parrySparksSpawnPoint.position, parrySparksSpawnPoint.rotation);
            }

            // Apply small knockback to BOTH player and enemy
            if (playerMovement != null)
            {
                playerMovement.ApplyKnockback(damageSource, knockbackDist * 0.5f, knockbackDur); // Half knockback for player
            }

            EnemyAI enemyAI = damageSource.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                enemyAI.ApplyKnockback(transform, knockbackDist * 0.5f, knockbackDur); // Half knockback for enemy
            }

            
            return;
        }

        // Cancel any current player attack.
        if (attackManager != null)
        {
            attackManager.CancelAttack();
        }

        // Apply damage.
        currentHealth -= damageAmount;
        Debug.Log($"Player took {damageAmount} heavy damage. Current Health: {currentHealth}");

        // Play the special heavy damage animation.
        animator.SetTrigger(animationTriggerHash);

        // Apply knockback to player
        if (playerMovement != null)
        {
            playerMovement.ApplyKnockback(damageSource, knockbackDist, knockbackDur);
        }

        // Play blood effect.
        if (bloodEffect != null && bloodEffectSpawnPoint != null)
        {
            ParticleSystem newBloodEffect = Instantiate(bloodEffect, bloodEffectSpawnPoint.position, Quaternion.identity);
            newBloodEffect.Play();
        }

        // Check for death.
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

    public IEnumerator SlowMoEffect()
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
        Destroy(gameObject); // Delay to allow death animation to play.
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
    public void CameraShake()
    {
        CameraShakerHandler.Shake(CameraShakeParry);
    }
    public void OpenDodgeWindow(string attackType)
    {
        isDodgeWindowActive = true;
        currentEnemyAttackType = attackType;

        // NEW: Reset the dodge input flag so player can dodge this new attack
        ZreyMovements playerMovement = GetComponent<ZreyMovements>();
        if (playerMovement != null)
        {
            playerMovement.ResetDodgeInput();  // Call new public method
        }

        Debug.Log($"<color=green>Dodge window OPEN for: {attackType}</color>");
    }
    public void CloseDodgeWindow()
    {
        isDodgeWindowActive = false;
        currentEnemyAttackType = "";
        Debug.Log("<color=red>Dodge window CLOSED</color>");
    }

    public bool IsDodgeWindowActive()
    {
        return isDodgeWindowActive;
    }

    public string GetCurrentEnemyAttackType()
    {
        return currentEnemyAttackType;
    }

    // ADD THIS FOR IMMUNITY:
    public bool IsDodgeActive()
    {
        return isDodgeActive;
    }

    public void SetDodgeActive(bool active)
    {
        isDodgeActive = active;
    }
    public void StartDodgeImmunity()
    {
        isDodgeActive = true;
        Debug.Log("<color=green>DODGE IMMUNITY STARTED (via animation event)</color>");
    }

    // NEW: Called by animation event at END of dodge animation
    public void StopDodgeImmunity()
    {
        isDodgeActive = false;
        Debug.Log("<color=red>DODGE IMMUNITY ENDED (via animation event)</color>");
    }
    // NEW: Play a random sound from an array
    private void PlayRandomSound(AudioClip[] soundArray)
    {
        if (audioSource == null || soundArray == null || soundArray.Length == 0)
        {
            Debug.LogWarning("AudioSource or sound array is not assigned!");
            return;
        }

        // Pick a random sound from the array
        int randomIndex = Random.Range(0, soundArray.Length);
        AudioClip selectedSound = soundArray[randomIndex];

        if (selectedSound != null)
        {
            audioSource.PlayOneShot(selectedSound);
            Debug.Log($"<color=cyan>Playing sound: {selectedSound.name}</color>");
        }
    }

    // NEW: Play a single sound
    private void PlaySound(AudioClip sound)
    {
        if (audioSource == null || sound == null)
        {
            Debug.LogWarning("AudioSource or sound is not assigned!");
            return;
        }

        audioSource.PlayOneShot(sound);
        Debug.Log($"<color=cyan>Playing sound: {sound.name}</color>");
    }
}
