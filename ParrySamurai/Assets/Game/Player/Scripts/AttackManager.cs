// AttackManager.cs (FINAL - Works with Kinematic Root Motion)

using UnityEngine;
using System.Collections;
using Cinemachine;
using UnityEngine.UI;

public class AttackManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private ZreyMovements playerMovement;

    [Header("Effects")]
    [SerializeField] private ParticleSystem slashEffect1;
    [SerializeField] private ParticleSystem slashEffect2;
    [SerializeField] private ParticleSystem slashEffect3;

    [Header("Combo Settings")]
    private int comboStep = 0;
    private bool isAttacking = false;
    private float lastAttackTime = 0f;
    [SerializeField] private float comboResetTime = 1f;

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
    private readonly int attack3TriggerHash = Animator.StringToHash("attack3");
    private readonly int attack4TriggerHash = Animator.StringToHash("attack4");
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
    [SerializeField] private float clashZoomLevel = 3.5f;
    [Tooltip("How fast the camera zooms in and out (in seconds).")]
    [SerializeField] private float zoomDuration = 0.5f; 
    private float originalZoomLevel;
    [Header("Clash Finisher Settings")]
    [Tooltip("The name of the CLASH finisher trigger for the player's animator.")]
    [SerializeField] private string clashFinisherTriggerName = "clashFinisher";
    [Tooltip("The name of the CLASH 'receive finisher' trigger for the enemy's animator.")]
    [SerializeField] private string clashReceiveFinisherTriggerName = "receiveClashFinisher";
    private int regularFinisherTriggerHash;
    private int regularReceiveFinisherTriggerHash;
    private int clashFinisherTriggerHash;
    private int clashReceiveFinisherTriggerHash;

    [Header("Clash QTE Settings")]
    [Tooltip("The parent GameObject for the QTE UI.")]
    [SerializeField] private GameObject qteUIParent; 
    [Tooltip("The UI Image element that will display the key sprite.")]
    [SerializeField] private UnityEngine.UI.Image qteKeyImage; 
    [Tooltip("The list of possible keys that can appear in the QTE.")]
    [SerializeField] private QTEKey[] possibleQTEKeys; 

    [Tooltip("How many prompts will appear in the sequence.")]
    [SerializeField] private int qteSequenceLength = 4; 
    [Tooltip("How many correct presses are needed to win.")]
    [SerializeField] private int qteRequiredWins = 3; 
    [Tooltip("How long the player has to press each key (in seconds).")]
    [SerializeField] private float qteTimePerKey = 0.6f;
    private bool isClashing = false;
    private Coroutine clashCoroutine;
    private int clashStateHash;
    [SerializeField] private ParticleSystem clashEffect;
    [SerializeField] private GameObject enemyWinEffectPrefab;
    [Tooltip("The position where the enemy's victory effect should spawn.")]
    [SerializeField] private Transform enemyWinEffectSpawnPoint;
    [Header("Clash Knockback - Enemy Wins")]
    [Tooltip("How far the PLAYER is knocked back when they lose.")]
    [SerializeField] private float playerKnockbackOnLoss = 5f;
    [Tooltip("How long the PLAYER's knockback lasts.")]
    [SerializeField] private float playerKnockbackDuration = 0.4f;
    [Tooltip("The small recoil distance for the ENEMY when they win.")]
    [SerializeField] private float enemyRecoilOnWin = 1.5f;
    [Tooltip("How long the ENEMY's recoil lasts.")]
    [SerializeField] private float enemyRecoilDuration = 0.2f;
    [Header("Attack 3 Heavy Knockback")]
    [SerializeField] private float attack3KnockbackDistance = 2.5f; // Bigger than normal
    [SerializeField] private float attack3KnockbackDuration = 0.25f;
   
    [SerializeField] private float chargeHoldTime = 0.5f; // Time to hold for full charge
    [SerializeField] private int chargeAttackDamage = 50; // Damage when charged
    [SerializeField] private float chargeStunDuration = 0.5f; // Stun duration
    [SerializeField] private float chargeKnockbackDistance = 3f; // Knockback distance
    [SerializeField] private float chargeKnockbackDuration = 0.3f; // Knockback duration
    private float chargeTimer = 0f;
    private bool isCharging = false;
    private readonly int chargeAttackTriggerHash = Animator.StringToHash("chargeAttack");
    [Header("Dash Attack Settings")]
    [SerializeField] private int dashAttackDamage = 15;  // Less than normal attack damage
    [SerializeField] private float dashAttackCooldown = 1.5f;  // Cooldown between dash attacks
    [SerializeField] private Transform dashAttackStartEffectSpawnPoint;  // Where to spawn start particles
    [SerializeField] private ParticleSystem dashAttackStartEffect;  // Particles at dash start position
    [SerializeField] private Transform dashAttackImpactEffectSpawnPoint;  // Where to spawn impact particles
    [SerializeField] private ParticleSystem dashAttackImpactEffect;  // Particles at teleport destination
    [SerializeField] private float dashAttackTeleportOffsetY = 0.5f;  // How far above enemy to teleport
    [SerializeField] private string dashAttackAnimationName = "dashAttack";  // Animation trigger name
    private readonly int dashAttackTriggerHash = Animator.StringToHash("dashAttack");
    private float lastDashAttackTime = 0f;  // Track cooldown
    private bool isDashAttackActive = false;  // State flag
    [Header("Sound Effects")]
    [SerializeField] private AudioSource audioSource;  // Audio source for player sounds
    [SerializeField] private AudioClip slashSound1;  // Slash 1 sound
    [SerializeField] private AudioClip slashSound2;  // Slash 2 sound
    [SerializeField] private AudioClip slashSound3;  // Slash 3 sound

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (playerMovement == null) playerMovement = GetComponent<ZreyMovements>();
        rb = GetComponent<Rigidbody2D>();
    }
    void Start()
    {
       
        clashFinisherTriggerHash = Animator.StringToHash(clashFinisherTriggerName);
        clashReceiveFinisherTriggerHash = Animator.StringToHash(clashReceiveFinisherTriggerName);

        clashStateHash = Animator.StringToHash("Clash");
    }
    void Update()
    {
        if (isPerformingFinisher || isClashing)
        {
            return;
        }
        if (isAttackDisabled)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && playerMovement.IsDashing() && !isAttacking && !isDashAttackActive)
        {
            // Check if player is dashing FORWARD
            if (playerMovement.IsDashingForward())
            {
                // Check cooldown
                if (Time.time - lastDashAttackTime >= dashAttackCooldown)
                {
                    TryPerformDashAttack();
                    return;  // Exit early to prevent normal attack
                }
            }
        }
        // Reset combo if too much time has passed
        if (Time.time - lastAttackTime > comboResetTime && comboStep > 0)
        {
            comboStep = 0;
        }

        // --- CHARGE ATTACK DETECTION (separate from normal attack) ---
        if (Input.GetMouseButtonDown(0))
        {
            // Check if we should start charging
            if (!isAttacking && playerMovement.IsGrounded() && !playerMovement.IsDashing() && !isAttackDisabled)
            {
                if (TryPerformFinisher()) return;
                if (playerHealth != null && playerHealth.isBlocking) return;

                // Start charging
                isCharging = true;
                chargeTimer = 0f;
                Debug.Log("<color=yellow>CHARGE ATTACK STARTED!</color>");
            }
        }

        // While holding mouse button - charge timer
        if (Input.GetMouseButton(0) && isCharging && !isAttacking)
        {
            chargeTimer += Time.deltaTime;

            // TRIGGER CHARGE ATTACK WHEN TIME IS REACHED
            if (chargeTimer >= chargeHoldTime)
            {
                Debug.Log("<color=orange>CHARGE ATTACK FULLY CHARGED - TRIGGERING NOW!</color>");
                PerformChargeAttack();
                isCharging = false;
                chargeTimer = 0f;
            }
        }

        // Release mouse button - cancel charge if not fully charged
        if (Input.GetMouseButtonUp(0) && isCharging && chargeTimer < chargeHoldTime)
        {
            // Charge was released before full charge - do normal attack instead
            if (!isAttacking && playerMovement.IsGrounded() && !playerMovement.IsDashing() && !isAttackDisabled)
            {
                if (TryPerformFinisher()) return;
                if (playerHealth != null && playerHealth.isBlocking) return;

                // IMMEDIATE NORMAL ATTACK
                lastAttackTime = Time.time;
                comboStep++;
                if (comboStep > 4)
                {
                    comboStep = 1;
                }
                PerformAttack(comboStep);
            }
            isCharging = false;
            chargeTimer = 0f;
        }
    }
    // NEW: Try to perform dash attack
    private void TryPerformDashAttack()
    {
        // Check if player has a locked-on enemy
        Transform lockedEnemy = playerMovement.GetLockedOnEnemy();

        if (lockedEnemy == null)
        {
            Debug.Log("<color=yellow>Dash attack: No locked-on enemy!</color>");
            return;
        }

        // Check if enemy is still alive
        EnemyHealth enemyHealth = lockedEnemy.GetComponent<EnemyHealth>();
        if (enemyHealth == null || enemyHealth.isDead)
        {
            Debug.Log("<color=yellow>Dash attack: Enemy is dead!</color>");
            return;
        }

        // SUCCESS: Perform the dash attack
        PerformDashAttack(lockedEnemy, enemyHealth);
    }

    // NEW: Execute the dash attack
    private void PerformDashAttack(Transform targetEnemy, EnemyHealth targetEnemyHealth)
    {
        Debug.Log("<color=cyan>DASH ATTACK TRIGGERED!</color>");

        lastDashAttackTime = Time.time;  // Set cooldown
        isDashAttackActive = true;
        isAttacking = true;
        playerMovement.SetAttacking(true);

        // 1. Spawn start effect at current position
        if (dashAttackStartEffect != null && dashAttackStartEffectSpawnPoint != null)
        {
            ParticleSystem startEffect = Instantiate(
                dashAttackStartEffect,
                dashAttackStartEffectSpawnPoint.position,
                dashAttackStartEffectSpawnPoint.rotation
            );
            startEffect.Play();
        }

        // 2. Calculate teleport position (above enemy)
        float horizontalOffset = playerMovement.IsFacingRight() ? dashAttackTeleportOffsetY : -dashAttackTeleportOffsetY;
        Vector3 teleportPosition = targetEnemy.position + new Vector3(horizontalOffset, 0, 0);

        // 3. Teleport player
        transform.position = teleportPosition;
        playerMovement.StopDashAttack();
        // 4. Spawn impact effect at teleport destination
        if (dashAttackImpactEffect != null && dashAttackImpactEffectSpawnPoint != null)
        {
            ParticleSystem impactEffect = Instantiate(
                dashAttackImpactEffect,
                dashAttackImpactEffectSpawnPoint.position,
                dashAttackImpactEffectSpawnPoint.rotation
            );
            impactEffect.Play();
        }

        animator.SetTrigger(dashAttackTriggerHash);

        // 6. Delay the flip so animation plays without lock-on interference
        StartCoroutine(DelayedFlipCoroutine(targetEnemy));

        // 7. Start coroutine to deal damage and cleanup
        StartCoroutine(DashAttackCoroutine(targetEnemyHealth));
    }
    private IEnumerator DelayedFlipCoroutine(Transform targetEnemy)
    {
        // Wait before flipping (let animation play)
        yield return new WaitForSeconds(0.6f);  // Adjust this value (0.6s or 1s)

        if (targetEnemy != null && playerMovement != null)
        {
            // NEW: Use the public method to force flip
            playerMovement.ForceFlip(targetEnemy);
            Debug.Log("<color=cyan>Delayed flip applied after dash attack!</color>");
        }
    }
    // NEW: Coroutine to handle dash attack damage and cleanup
    private IEnumerator DashAttackCoroutine(EnemyHealth targetEnemyHealth)
    {
        // NEW: Set flag to prevent flip glitch
        playerMovement.SetDashAttackJustFinished(true);

        // Wait for animation to reach damage frame
        yield return new WaitForSeconds(0.3f);

        // Deal damage
        if (targetEnemyHealth != null && !targetEnemyHealth.isDead)
        {
            Debug.Log("<color=cyan>DASH ATTACK HIT! Dealing damage!</color>");
            targetEnemyHealth.TakeDamage(dashAttackDamage, this);
        }

        // Wait for animation to finish
        yield return new WaitForSeconds(0.5f);

        // Cleanup
        isDashAttackActive = false;
        isAttacking = false;
        playerMovement.SetAttacking(false);
        comboStep = 0;

        // NEW: Wait a bit more for rotation to settle, then allow flips again
        yield return new WaitForSeconds(0.3f);
        playerMovement.SetDashAttackJustFinished(false);

        Debug.Log("<color=yellow>Dash attack finished - ready for next action!</color>");
    }
    private void PerformChargeAttack()
    {
        isAttacking = true;
        playerMovement.SetAttacking(true);
        animator.SetTrigger(chargeAttackTriggerHash);
        comboStep = 0; // Reset combo
        lastAttackTime = Time.time;

        Debug.Log("<color=red>CHARGE ATTACK ANIMATION TRIGGERED!</color>");
    }

    // Call this from animation event when charge attack hits
   public void DealChargeAttackDamage()
{
    Collider2D[] enemiesHit = Physics2D.OverlapBoxAll(attackDamagePoint.position, attackDamageAreaSize, 0f, enemyLayer);

    foreach (Collider2D enemy in enemiesHit)
    {
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            Debug.Log("<color=red>CHARGE ATTACK HIT!</color>");

            // Deal damage
            enemyHealth.TakeDamage(chargeAttackDamage, this);

            // Play getParried animation
            Animator enemyAnimator = enemyHealth.GetComponent<Animator>();
            if (enemyAnimator != null)
            {
               
                enemyAnimator.SetTrigger(Animator.StringToHash("getParried"));
                Debug.Log("<color=cyan>Playing getParried animation!</color>");
            }

            // Stun the enemy
            StartCoroutine(StunEnemyCoroutine(enemyHealth));

            // Apply knockback
            EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                enemyAI.ApplyKnockback(transform, chargeKnockbackDistance, chargeKnockbackDuration);
            }
        }
    }
}

    private IEnumerator StunEnemyCoroutine(EnemyHealth enemyHealth)
    {
        // Play getParried animation
        Animator enemyAnimator = enemyHealth.GetComponent<Animator>();
        if (enemyAnimator != null)
        {
            enemyAnimator.SetTrigger(Animator.StringToHash("getParried"));
        }

        // Stun for specified duration
        yield return new WaitForSeconds(chargeStunDuration);
    }
    public void StartClash(EnemyAI enemy)
    {
        // CLASH REMOVED - Just apply knockback instead
        Debug.Log("<color=yellow>Heavy parry! Applying knockback to both.</color>");

        if (playerMovement != null && enemy != null)
        {
            // Small knockback for both
            playerMovement.ApplyKnockback(enemy.transform, 1.5f, 0.2f);
            enemy.ApplyKnockback(transform, 1.5f, 0.2f);
        }
    }

    private IEnumerator ClashSequence(EnemyAI enemy)
    {
        Debug.Log("<color=yellow>--- CLASH SEQUENCE COROUTINE STARTED ---</color>");
        isClashing = true;
        if (clashEffect != null)
        {
            clashEffect.Play();
        }
        // --- Phase 2: The Lockdown ---
        playerMovement.SetAttacking(true);
        enemy.SetClashState(true);

        // --- Camera and Effects (Your existing code is good) ---
        if (cameraFollowScript != null && Camera.main != null)
        {
            originalZoomLevel = Camera.main.orthographicSize;
            cameraFollowScript.TriggerZoom(clashZoomLevel, zoomDuration);
        }
       

        // --- THE GUARANTEED ANIMATION FIX ---
        // 1. Set the boolean flag.
        animator.SetBool("isClashing", true);
        enemy.GetComponent<Animator>().SetBool("isClashing", true);

        // 2. Wait for the end of the current frame. This allows any rogue triggers
        //    (like the parry one we just disabled) to finish their business.
        yield return new WaitForEndOfFrame();

        // 3. NOW, on a clean slate, we force the animation state. This is our override.
        animator.Play(clashStateHash);
        enemy.GetComponent<Animator>().Play("Clash");
        Debug.Log("<color=yellow>Forcing Player and Enemy into 'Clash' animation state.</color>");
        // --- END OF ANIMATION FIX ---

        // Activate the UI Parent Canvas
        if (qteUIParent != null) qteUIParent.SetActive(true);

        // A small delay before the QTE starts, allowing animations to blend in.
        yield return new WaitForSeconds(0.5f);

        // --- Phase 3: The Mini-Game ---
        int correctPresses = 0;
        for (int i = 0; i < qteSequenceLength; i++)
        {
            if (possibleQTEKeys.Length == 0)
            {
                Debug.LogError("QTE FAILED: No possible QTE keys are assigned in the AttackManager!");
                break;
            }
            QTEKey currentKey = possibleQTEKeys[Random.Range(0, possibleQTEKeys.Length)];

            // --- THE GUARANTEED UI FIX ---
            if (qteKeyImage != null)
            {
                // 1. Assign the sprite.
                qteKeyImage.sprite = currentKey.keySprite;

                // 2. BRUTE FORCE a fully visible color. This overrides any transparency.
                qteKeyImage.color = new Color(1f, 1f, 1f, 1f); // White, 100% Opaque

                // 3. Enable the image object.
                qteKeyImage.gameObject.SetActive(true);
                Debug.Log($"Showing QTE Key: {currentKey.keyName}");
            }
            // --- END OF UI FIX ---

            float timer = 0f;
            bool pressedCorrectly = false;
            bool pressedWrong = false;

            while (timer < qteTimePerKey)
            {
                if (Input.GetKeyDown(currentKey.keyCode))
                {
                    pressedCorrectly = true;
                    break;
                }
                if (Input.anyKeyDown && !Input.GetKeyDown(currentKey.keyCode))
                {
                    pressedWrong = true;
                    break;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            if (qteKeyImage != null) qteKeyImage.gameObject.SetActive(false);

            if (pressedCorrectly) { correctPresses++; Debug.Log($"<color=green>QTE Correct! ({correctPresses}/{qteRequiredWins})</color>"); }
            else if (pressedWrong) { Debug.Log("<color=red>QTE Wrong Key!</color>"); }
            else { Debug.Log("<color=orange>QTE Timed Out!</color>"); }

            yield return new WaitForSeconds(0.2f);
        }

        // --- Phase 4 & 5: Judgment and Release ---
        FinishClash(enemy, correctPresses);
    }

    private void FinishClash(EnemyAI enemy, int correctPresses)
    {
        Debug.Log("<color=cyan>--- CLASH JUDGMENT ---</color>");

        // --- PATH A: PLAYER WINS ---
        if (correctPresses >= qteRequiredWins)
        {
            Debug.Log("<color=green>PLAYER WINS CLASH! Transitioning directly to finisher...</color>");

            // 1. Get the EnemyHealth component.
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
            {
                Debug.LogError("FATAL: Cannot start finisher, EnemyHealth is null.");
                // If this fails, we must fall through to the cleanup code below.
            }
            else
            {
                // 2. Start the finisher sequence.
                StartCoroutine(ClashFinisherSequence(enemyHealth));

                // 3. IMPORTANT: RETURN. Do NOT run any of the cleanup code below.
                //    The finisher sequence is now in full control.
                return;
            }
        }

        // --- PATH B: ENEMY WINS (This code only runs if the player lost) ---
        Debug.Log("<color=red>ENEMY WINS CLASH! Applying punishment and cleaning up.</color>");

        // Apply punishment effects and knockbacks.
        // ... (Your existing enemy win logic is perfect here) ...
        if (enemyWinEffectPrefab != null && enemyWinEffectSpawnPoint != null) { /* ... */ }
        if (playerMovement != null) { playerMovement.ApplyKnockback(enemy.transform, playerKnockbackOnLoss, playerKnockbackDuration); }
        if (enemy != null) { enemy.ApplyKnockback(this.transform, enemyRecoilOnWin, enemyRecoilDuration); }


        // --- FULL CLASH CLEANUP (Only runs on ENEMY win) ---
        Debug.Log("Cleaning up clash state after enemy victory.");

        // Manually clean up the enemy's combo state.
        EnemyAttack enemyAttack = enemy.GetComponent<EnemyAttack>();
        if (enemyAttack != null) { enemyAttack.FinishCombo(); }

        // Stop effects and UI.
        if (clashEffect != null) { clashEffect.Stop(); }
        if (qteUIParent != null) { qteUIParent.SetActive(false); }

        // Reset camera zoom.
        if (cameraFollowScript != null) { cameraFollowScript.TriggerZoom(originalZoomLevel, zoomDuration); }

        // Stop looping animations.
        animator.SetBool("isClashing", false);
        if (enemy != null) { enemy.GetComponent<Animator>().SetBool("isClashing", false); }

        // Unlock characters.
        playerMovement.SetAttacking(false);
        if (enemy != null) { enemy.SetClashState(false); }

        isClashing = false;
    }
    private IEnumerator ClashFinisherSequence(EnemyHealth targetEnemy)
    {
        Debug.Log("--- CLASH FINISHER SEQUENCE STARTED (inheriting clash state) ---");
        isPerformingFinisher = true;
        currentFinisherTarget = targetEnemy;
        targetEnemy.MarkAsFinished(); // This also sets the enemy's isDead flag now.


        playerMovement.FlipTowards(targetEnemy.transform);

        // Trigger the animations.
        animator.SetTrigger(clashFinisherTriggerHash);
        targetEnemy.GetComponent<Animator>().SetTrigger(clashReceiveFinisherTriggerHash);

        yield return null;
    }
    public void OnClashFinisherComplete()
    {
        Debug.Log("<color=green>FINISHER COMPLETE. Executing the enemy.</color>");

        // --- THIS IS THE GUARANTEED FIX ---
        // 1. Check if we have a valid target to kill.
        if (currentFinisherTarget != null)
        {
            // The Die() method has already been called by MarkAsFinished(), but we can ensure it.
            currentFinisherTarget.Die();
            // Destroy the enemy's GameObject.
            Destroy(currentFinisherTarget.gameObject, 1f);
            Debug.Log($"Destroying {currentFinisherTarget.name}.");
        }
        if (playerMovement != null)
        {
           
            playerMovement.SetAttacking(false);
            Debug.Log("PlayerMovement state has been reset.");
        }
        isPerformingFinisher = false;
        currentFinisherTarget = null; // Set to null AFTER we've used it.

        if (cameraFollowScript != null)
        {
            // We need to use the original zoom level we stored when the finisher started.
            cameraFollowScript.TriggerZoom(originalZoomLevel, zoomDuration);
            Debug.Log("Camera zoom has been reset.");
        }
        isPerformingFinisher = false;
        isClashing = false; // Reset this too, just to be 100% safe.
        currentFinisherTarget = null;
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
        targetEnemy.MarkAsFinished();
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
        isAttacking = false;
        comboStep = 0;
        lastAttackTime = 0f;

        if (playerMovement != null)
        {
            playerMovement.SetAttacking(false);
        }

        animator.Play("Idle");
    }


    private void PerformAttack(int step)
    {
        // INSTANT STATE CHANGE
        isAttacking = true;
        playerMovement.SetAttacking(true);

        if (step == 1)
        {
            animator.SetTrigger(attack1TriggerHash);
        }
        else if (step == 2)
        {
            animator.SetTrigger(attack2TriggerHash);
        }
        else if (step == 3)
        {
            animator.SetTrigger(attack3TriggerHash);
        }
        else if (step == 4)  
        {
            animator.SetTrigger(attack4TriggerHash);
        }
        Debug.Log($"<color=green>ATTACK {step} TRIGGERED IMMEDIATELY!</color>");
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


    public void EndAttack()
    {
        isAttacking = false;
        playerMovement.SetAttacking(false);
        Debug.Log($"Attack {comboStep} ended. Ready for next input.");
    }

    public void TriggerSlashEffect1()
    {
        if (slashEffect1 != null) slashEffect1.Play();
    }

    public void TriggerSlashEffect2()
    {
        if (slashEffect2 != null) slashEffect2.Play();
    }
    public void TriggerSlashEffect3()
    {
        if (slashEffect3 != null) slashEffect3.Play();
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
                // If this is attack 3 or 4, apply extra knockback
                if (comboStep == 3)
                {
                    Debug.Log("<color=red>ATTACK 3 HIT! Applying heavy knockback to enemy!</color>");
                    enemyHealth.TakeDamage(attackDamage, this);

                    
                }
                else if (comboStep == 4)  // NEW
                {
                    Debug.Log("<color=red>ATTACK 4 HIT! Applying MASSIVE knockback to enemy!</color>");
                    enemyHealth.TakeDamage(attackDamage, this);

                    // Apply knockback to enemy
                    EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
                    if (enemyAI != null)
                    {
                        enemyAI.ApplyKnockback(transform, attack3KnockbackDistance, attack3KnockbackDuration);
                    }
                }
                else
                {
                    enemyHealth.TakeDamage(attackDamage, this);
                }

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
    public bool IsPerformingFinisher()
    {
        return isPerformingFinisher;
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
    public void PlaySlashSound1()
    {
        if (audioSource == null || slashSound1 == null)
        {
            Debug.LogWarning("AudioSource or Slash Sound 1 is not assigned!");
            return;
        }

        audioSource.PlayOneShot(slashSound1);
        Debug.Log("<color=cyan>Playing slash sound 1!</color>");
    }

    // NEW: Play slash sound 2
    public void PlaySlashSound2()
    {
        if (audioSource == null || slashSound2 == null)
        {
            Debug.LogWarning("AudioSource or Slash Sound 2 is not assigned!");
            return;
        }

        audioSource.PlayOneShot(slashSound2);
        Debug.Log("<color=cyan>Playing slash sound 2!</color>");
    }

    // NEW: Play slash sound 3
    public void PlaySlashSound3()
    {
        if (audioSource == null || slashSound3 == null)
        {
            Debug.LogWarning("AudioSource or Slash Sound 3 is not assigned!");
            return;
        }

        audioSource.PlayOneShot(slashSound3);
        Debug.Log("<color=cyan>Playing slash sound 3!</color>");
    }
}
