// EnemyHealth.cs

using FirstGearGames.SmoothCameraShaker;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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
    public bool isDead { get; private set; } = false;
    private bool isPerformingCounterAttack = false;
    private readonly int frontKickTriggerHash = Animator.StringToHash("frontKick");
    [SerializeField] private ParticleSystem getParriedSparksEffect;
    [Header("Posture System")]
    [SerializeField] private float maxPosture = 100f;  // Max posture value
    private float currentPosture = 0f;  // Current posture
    [SerializeField] private float postureIncreaseOnParry = 25f;  // Increase when parried
    [SerializeField] private float postureIncreaseOnEnemyParry = 15f;  // Increase when enemy parries
    [SerializeField] private float postureDecayRate = 5f;  // Decay per second
    [SerializeField] private float postureDecayDelay = 2f;  // Delay before decay starts
    private float lastPostureChangeTime = 0f;  // Track when posture last changed
    [Header("UI References")]
    [SerializeField] private Slider healthBarSlider;
    [SerializeField] private Text healthBarText;
    [SerializeField] private Slider postureBarSliderLeft;   // NEW: Left half
    [SerializeField] private Slider postureBarSliderRight;  // NEW: Right half
    [SerializeField] private Text postureBarText;
    [SerializeField] private Image postureFullIndicator;
    [Header("Slider Animation Settings")]
    [SerializeField] private float sliderAnimationSpeed = 5f;
    private Coroutine healthSliderCoroutine;
    private Coroutine postureSliderLeftCoroutine;   // NEW
    private Coroutine postureSliderRightCoroutine;  // NEW
    [Header("Sound Effects")]
    [SerializeField] private AudioSource audioSource;  // Audio source for enemy sounds
    [SerializeField] private AudioClip[] enemyParrySounds = new AudioClip[2];  // 2 enemy parry sounds
    [SerializeField] private AudioClip[] enemyHitSounds = new AudioClip[3];  // Sound when enemy takes damage
    [SerializeField] private AudioClip finisherSound;
    void Awake()
    {
        currentHealth = maxHealth;
        currentPosture = 0f;
        animator = GetComponent<Animator>();
        followScript = GetComponent<EnemyFollow>();
        rb = GetComponent<Rigidbody2D>();
        enemyAI = GetComponent<EnemyAI>();

        if (finisherPromptUI != null)
        {
            finisherPromptUI.SetActive(false);
        }

        if (healthBarSlider != null)
        {
            healthBarSlider.minValue = 0;
            healthBarSlider.maxValue = 1;
            healthBarSlider.value = 1;
        }

        // NEW: Initialize both posture sliders
        if (postureBarSliderLeft != null)
        {
            postureBarSliderLeft.minValue = 0;
            postureBarSliderLeft.maxValue = 1;
            postureBarSliderLeft.value = 0;
        }

        if (postureBarSliderRight != null)
        {
            postureBarSliderRight.minValue = 0;
            postureBarSliderRight.maxValue = 1;
            postureBarSliderRight.value = 0;
        }

        UpdateBars();
    }
    private void UpdateBars()
    {
        // Update health bar with animation
        if (healthBarSlider != null)
        {
            float healthPercent = (float)currentHealth / maxHealth;

            if (healthSliderCoroutine != null)
            {
                StopCoroutine(healthSliderCoroutine);
            }

            healthSliderCoroutine = StartCoroutine(AnimateSlider(healthBarSlider, healthPercent));
        }

        if (healthBarText != null)
        {
            healthBarText.text = $"{currentHealth}/{maxHealth}";
        }

        // NEW: Update posture bars (both halves)
        if (postureBarSliderLeft != null || postureBarSliderRight != null)
        {
            float posturePercent = currentPosture / maxPosture;

            // Stop previous animations
            if (postureSliderLeftCoroutine != null)
            {
                StopCoroutine(postureSliderLeftCoroutine);
            }
            if (postureSliderRightCoroutine != null)
            {
                StopCoroutine(postureSliderRightCoroutine);
            }

            // Animate both halves simultaneously
            if (postureBarSliderLeft != null)
            {
                postureSliderLeftCoroutine = StartCoroutine(AnimateSlider(postureBarSliderLeft, posturePercent));
            }
            if (postureBarSliderRight != null)
            {
                postureSliderRightCoroutine = StartCoroutine(AnimateSlider(postureBarSliderRight, posturePercent));
            }
        }

        if (postureBarText != null)
        {
            postureBarText.text = $"{Mathf.RoundToInt(currentPosture)}/{Mathf.RoundToInt(maxPosture)}";
        }
    }
    // NEW: Disable all UI bars when entering finishable state
    private void DisableUIBars()
    {
        if (healthBarSlider != null)
        {
            healthBarSlider.gameObject.SetActive(false);
        }

        if (postureBarSliderLeft != null)
        {
            postureBarSliderLeft.gameObject.SetActive(false);
        }

        if (postureBarSliderRight != null)
        {
            postureBarSliderRight.gameObject.SetActive(false);
        }

        if (healthBarText != null)
        {
            healthBarText.gameObject.SetActive(false);
        }

        if (postureBarText != null)
        {
            postureBarText.gameObject.SetActive(false);
        }

        if (postureFullIndicator != null)
        {
            postureFullIndicator.gameObject.SetActive(false);
        }

        Debug.Log("<color=yellow>All UI bars DISABLED!</color>");
    }
    // NEW: Smooth slider animation
    private IEnumerator AnimateSlider(Slider slider, float targetValue)
    {
        float startValue = slider.value;
        float elapsedTime = 0f;
        float duration = 1f / sliderAnimationSpeed;  // Duration based on speed

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;

            // Use smooth easing (ease-out)
            progress = 1f - Mathf.Pow(1f - progress, 3f);

            slider.value = Mathf.Lerp(startValue, targetValue, progress);
            yield return null;
        }

        // Ensure final value is exact
        slider.value = targetValue;
    }
    void Update()
    {
        // NEW: Decay posture over time
        DecayPosture();
    }
    // NEW: Increase posture
    private void IncreasePosture(float amount)
    {
        currentPosture += amount;
        currentPosture = Mathf.Clamp(currentPosture, 0, maxPosture);
        lastPostureChangeTime = Time.time;

        Debug.Log($"<color=orange>Posture increased by {amount}! Current: {currentPosture}/{maxPosture}</color>");

        UpdateBars();

        if (currentPosture >= maxPosture)
        {
            Debug.Log("<color=red>POSTURE FULL! Enemy is now finishable!</color>");
            ShowPostureFullIndicator();  // NEW: Show the indicator
        }
    }
    private void ShowPostureFullIndicator()
    {
        if (postureFullIndicator != null)
        {
            postureFullIndicator.gameObject.SetActive(true);
            Debug.Log("<color=yellow>Posture Full Indicator ENABLED!</color>");
        }
    }

    // NEW: Hide posture full indicator (optional - for when posture decays)
    private void HidePostureFullIndicator()
    {
        if (postureFullIndicator != null)
        {
            postureFullIndicator.gameObject.SetActive(false);
            Debug.Log("<color=grey>Posture Full Indicator DISABLED!</color>");
        }
    }
    // NEW: Decay posture over time
    private void DecayPosture()
    {
        if (currentPosture <= 0) return;

        if (Time.time - lastPostureChangeTime > postureDecayDelay)
        {
            currentPosture -= postureDecayRate * Time.deltaTime;
            currentPosture = Mathf.Max(0, currentPosture);
            UpdateBars();

            // NEW: Hide indicator if posture drops below max
            if (currentPosture < maxPosture)
            {
                HidePostureFullIndicator();
            }
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
        isParrying = true;
        isPerformingCounterAttack = false; // Regular parry, not a counter
    }
    public void StartCounterAttackState()
    {
        isParrying = false;
        isPerformingCounterAttack = true;
        Debug.Log("<color=red>COUNTER-ATTACK STATE ACTIVE</color>");
    }
    public void TakeDamage(int damageAmount, AttackManager playerAttackManager = null)
    {
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
                PlayRandomSound(enemyParrySounds);

                if (currentPosture >= maxPosture)
                {
                    Debug.Log("<color=red>POSTURE FULL! Entering finishable state!</color>");
                    EnterFinishableState();
                    return;
                }
                IncreasePosture(postureIncreaseOnEnemyParry);
                if (enemyAI != null) { enemyAI.OnSuccessfulParry(); }
                CameraShakerHandler.Shake(CameraShakeParry);
                if (parrySparksEffect != null) { Instantiate(parrySparksEffect, parrySparksSpawnPoint.position, Quaternion.identity); }
                EnemyFollow followScript = GetComponent<EnemyFollow>();
                if (followScript != null)
                {
                    followScript.CancelLunge();
                }
                ZreyMovements playerMovement = FindObjectOfType<ZreyMovements>();
                if (playerMovement != null)
                {
                    playerMovement.GetParried(this.transform);
                    if (enemyAI != null)
                    {
                        // KNOCKBACK DIRECTION: AWAY from player (not toward)
                        Vector2 knockbackDirection = (transform.position - playerMovement.transform.position).normalized;
                        enemyAI.ApplyKnockback(playerMovement.transform, parryKnockbackForce, parryKnockbackDuration);
                    }
                }
               
                // If we parry, we stop everything. Do not take damage.
                return;
            }
        }
     
        Debug.Log("<color=red>DAMAGE PHASE: Enemy is taking damage.</color>");
        currentHealth -= damageAmount;
        UpdateBars();
        PlayRandomSound(enemyHitSounds);
        if (currentPosture >= maxPosture)
        {
            Debug.Log("<color=red>POSTURE FULL! Entering finishable state!</color>");
            EnterFinishableState();
            return;
        }

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
    private void EnterFinishableState()
    {
        if (isFinishable) return;  // Already finishable

        Debug.Log("<color=orange>Enemy posture full! Entering FINISHABLE state.</color>");
        isFinishable = true;

        // --- LOCK DOWN THE ENEMY COMPLETELY ---
        if (GetComponent<EnemyAI>() != null) GetComponent<EnemyAI>().enabled = false;
        if (GetComponent<EnemyFollow>() != null) GetComponent<EnemyFollow>().enabled = false;
        if (GetComponent<EnemyAttack>() != null) GetComponent<EnemyAttack>().enabled = false;

        rb.velocity = Vector2.zero;
        rb.isKinematic = true;

        animator.SetTrigger(enterFinishableStateTriggerHash);
        animator.applyRootMotion = false;

        gameObject.layer = LayerMask.NameToLayer("Finishable");

        if (finisherPromptUI != null)
        {
            finisherPromptUI.SetActive(true);
        }

        Debug.Log("<color=red>ENEMY IS NOW FINISHABLE - ALL BEHAVIOR LOCKED!</color>");
        DisableUIBars();
    }
    public bool IsInUninterruptibleCombo()
    {
        return isInCombo;
    }
    public void EndCounterAttackState()
    {
        isPerformingCounterAttack = false;
        Debug.Log("<color=grey>Counter-attack state ended</color>");
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
    public void GetParried(Transform parryingPlayer)
    {
        if (parryingPlayer == null) return;

        // NEW: Increase posture when parried
        IncreasePosture(postureIncreaseOnParry);

        // ONLY stun if this was a counter-attack
        if (isPerformingCounterAttack)
        {
            Debug.Log("<color=red>Counter-attack was parried! Applying stun.</color>");
            StartCoroutine(StunCoroutine(parryingPlayer));
            isPerformingCounterAttack = false;
        }
        else
        {
            Debug.Log("<color=yellow>Regular attack was parried, but no stun (not a counter)</color>");
            isParrying = false;
        }
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
    public void Die()
    {
        if (isDead) return;

        Debug.Log($"<color=red>{gameObject.name} has been slain!</color>");
        isDead = true;

        if (isFinishable)
        {
            return;
        }

        // If health reaches 0 but posture isn't full, still enter finishable
        EnterFinishableState();
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
        isDead = true;
        // It's also a good idea to hide the UI prompt immediately.
        if (finisherPromptUI != null)
        {
            finisherPromptUI.SetActive(false);
        }
    }
    public void DealFrontKickDamage()
    {
        // Find the player
        ZreyMovements playerMovement = FindObjectOfType<ZreyMovements>();
        PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerMovement != null && playerHealth != null)
        {
            Debug.Log("<color=orange>FRONT KICK HIT! Knocking back player!</color>");

            // Apply knockback to player
            float kickKnockbackDistance = 3f; // Adjust this value
            float kickKnockbackDuration = 0.3f; // Adjust this value
            playerMovement.ApplyKnockback(this.transform, kickKnockbackDistance, kickKnockbackDuration);

            // Play player's knockback animation
            Animator playerAnimator = playerMovement.GetComponent<Animator>();
            if (playerAnimator != null)
            {
                playerAnimator.SetTrigger("KnockBack"); // Or create a new "Knockback" trigger
            }

            // Optional: Deal small damage
            // playerHealth.TakeDamage(10, null);
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

    public void PlayParrySparks()
    {
        if (getParriedSparksEffect != null && parrySparksSpawnPoint != null)
        {
            Instantiate(getParriedSparksEffect, parrySparksSpawnPoint.position, Quaternion.identity);
            Debug.Log("<color=cyan>Parry sparks played!</color>");
        }
    }
    private void PlayRandomSound(AudioClip[] soundArray)
    {
        if (audioSource == null || soundArray == null || soundArray.Length == 0)
        {
            return;
        }

        int randomIndex = Random.Range(0, soundArray.Length);
        AudioClip selectedSound = soundArray[randomIndex];

        if (selectedSound != null)
        {
            audioSource.PlayOneShot(selectedSound);
            Debug.Log($"<color=cyan>Enemy playing sound: {selectedSound.name}</color>");
        }
    }

    // NEW: Play a single sound
    private void PlaySound(AudioClip sound)
    {
        if (audioSource == null || sound == null)
        {
            return;
        }

        audioSource.PlayOneShot(sound);
        Debug.Log($"<color=cyan>Enemy playing sound: {sound.name}</color>");
    }
    // NEW: Play finisher sound
    public void PlayFinisherSound()
    {
        if (audioSource == null || finisherSound == null)
        {
            Debug.LogWarning("AudioSource or Finisher Sound is not assigned!");
            return;
        }

        audioSource.PlayOneShot(finisherSound);
        Debug.Log("<color=orange>Playing finisher sound!</color>");
    }
}
