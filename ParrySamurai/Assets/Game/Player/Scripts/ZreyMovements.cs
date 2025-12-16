// ZreyMovements.cs (FINAL - Works WITH Apply Root Motion)

using FirstGearGames.Utilities.Objects;
using System.Collections;
using UnityEngine;

public class ZreyMovements : MonoBehaviour
{
    // --- PUBLIC STATE FOR OTHER SCRIPTS ---
    [HideInInspector] public bool isLungeActive = false;
    [HideInInspector] public Vector2 lungeVelocity;

    // --- PRIVATE STATE & COMPONENTS ---
    private enum MovementState { Idle, Running, Jumping, Falling, Landing, CombatIdle, CombatMoveForward, CombatMoveBackward, Dashing }
    private MovementState currentState;

    [Header("Components")]
    private Rigidbody2D rb;
    private Animator animator;
    [SerializeField] private ParticleSystem breathEffect;
    [SerializeField] private ParticleSystem SmokeEffect;

    [Header("Walk Mode Settings")]
    [SerializeField] private float walkMoveSpeed = 8f;

    [Header("Combat Mode Settings")]
    [SerializeField] private bool isCombatMode = false;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    private float dashTimer;
    private Vector2 dashDirection;

    [Header("Double Tap Settings")]
    [SerializeField] private float doubleTapTimeThreshold = 0.3f;
    private float lastTapTime_Right = -1f;
    private float lastTapTime_Left = -1f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 16f;
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    private bool isGrounded;

    [Header("Flip Settings")]
    [SerializeField] private Vector3 rightFacingRotation = new Vector3(0, -137, 0);
    [SerializeField] private Vector3 leftFacingRotation = new Vector3(0, -222, 180);
    private bool isFacingRight = true;

    private float horizontalInput;
    private bool isAttackLocked = false;

    // --- ANIMATION HASHES ---
    private readonly int isRunningHash = Animator.StringToHash("isRunning");
    private readonly int jumpTriggerHash = Animator.StringToHash("jump");
    private readonly int isFallingHash = Animator.StringToHash("isFalling");
    private readonly int landTriggerHash = Animator.StringToHash("land");
    private readonly int isCombatModeHash = Animator.StringToHash("isCombatMode");
    private readonly int isMovingForwardHash = Animator.StringToHash("isMovingForward");
    private readonly int isMovingBackwardHash = Animator.StringToHash("isMovingBackward");
    private readonly int dashForwardTriggerHash = Animator.StringToHash("dashForward");
    private readonly int dashBackwardTriggerHash = Animator.StringToHash("dashBackward");

    [SerializeField] private float combatMoveSpeed = 5f;
    [SerializeField] private float momentumDuration = 0.2f;
    private Coroutine momentumCoroutine;
    [SerializeField] private float getParriedKnockbackDistance = 2f;
    [Tooltip("How long the knockback effect lasts on the player.")]
    [SerializeField] private float getParriedKnockbackDuration = 0.2f;
    // This variable will control if velocity is applied in combat mode.
    private bool canCombatMove = false;
    private PlayerHealth playerHealth;
    private bool isBeingKnockedBack = false;
    [SerializeField] private AnimationCurve knockbackCurve;
    private AttackManager attackManager;
    private bool dodgeInputConsumed = false;
    private Transform lockedOnEnemy = null;
    private bool isLockedOn = false;
    private bool isDashAttackJustFinished = false;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        playerHealth = GetComponent<PlayerHealth>();
        attackManager = GetComponent<AttackManager>();
    }

    void OnAnimatorMove()
    {
        // If an attack is happening, we are in charge, not the Animator.
        if (isAttackLocked)
        {
            // By returning here, we are telling the Animator:
            // "Do NOT apply your root motion this frame."
            // This allows our lunge's rb.MovePosition() command to work without a fight.
            return;
        }

        // If no attack is happening, let the Animator do its job.
        // This makes the combat walk work correctly.
        transform.position = animator.rootPosition;
        transform.rotation = animator.rootRotation;
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);

        HandleModeSwitch();
        HandleDashInput();
        UpdateState();
        FlipCharacter(); // Call the flip every frame to ensure it's always correct.
    }

    void FixedUpdate()
    {
        if (isBeingKnockedBack)
        {
            return;
        }
        if (playerHealth != null && playerHealth.IsCurrentlyBlocking())
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
            return; // Stop here.
        }
        if (currentState == MovementState.Dashing)
        {
            if (isAttackLocked)
            {
                // Dash was interrupted, exit dash state
                SwitchState(MovementState.CombatIdle);
                rb.velocity = Vector2.zero;  // Clear velocity
                Debug.Log("<color=red>Dash interrupted - velocity cleared!</color>");
                return;
            }

            rb.MovePosition(rb.position + dashDirection * dashSpeed * Time.fixedDeltaTime);
            return;
        }
        if (!isCombatMode && !isAttackLocked)
        {
            rb.velocity = new Vector2(horizontalInput * walkMoveSpeed, rb.velocity.y);
        }
        // If an attack is happening, lock horizontal movement.
        if (isAttackLocked)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
            return;
        }

        // If a momentum coroutine is running, it has full control of the velocity.
        if (momentumCoroutine != null)
        {
            return;
        }

        if (isCombatMode)
        {
            if (canCombatMove)
            {
                // Keys are ABSOLUTE directions:
                // D = always move RIGHT
                // A = always move LEFT

                float moveSpeed = 0f;

                if (horizontalInput > 0) // D pressed = move RIGHT
                {
                    moveSpeed = combatMoveSpeed;
                }
                else if (horizontalInput < 0) // A pressed = move LEFT
                {
                    moveSpeed = -combatMoveSpeed;
                }

                // Slower when moving backward (opposite to facing direction)
                if ((isFacingRight && moveSpeed < 0) || (!isFacingRight && moveSpeed > 0))
                {
                    moveSpeed *= 0.8f; // Backward movement is slower
                }

                rb.velocity = new Vector2(moveSpeed, rb.velocity.y);
            }
            else
            {
                rb.velocity = new Vector2(0, rb.velocity.y);
            }
        }
        else // Walk Mode
        {
            rb.velocity = new Vector2(horizontalInput * walkMoveSpeed, rb.velocity.y);
        }
    }

    // --- PUBLIC METHODS FOR OTHER SCRIPTS ---
    public void SetAttacking(bool attacking)
    {
        isAttackLocked = attacking;
    }

    public bool IsFacingRight()
    {
        return isFacingRight;
    }
    public void StartCombatMovement()
    {
        // If a momentum coroutine is running, stop it immediately.
        if (momentumCoroutine != null)
        {
            StopCoroutine(momentumCoroutine);
            momentumCoroutine = null;
        }
        // Open the gate to allow movement in FixedUpdate.
        canCombatMove = true;
    }

    public void StopCombatMovement()
    {
        // Close the movement gate.
        canCombatMove = false;

        // Start the momentum coroutine to handle the slowdown.
        if (gameObject.activeInHierarchy && momentumCoroutine == null)
        {
            momentumCoroutine = StartCoroutine(MomentumCoroutine());
        }
    }

    private IEnumerator MomentumCoroutine()
    {
        float timer = 0f;
        // Get the velocity at the exact moment we start stopping.
        Vector2 startVelocity = rb.velocity;

        while (timer < momentumDuration)
        {
            // Smoothly decrease the velocity from its starting value to zero.
            rb.velocity = Vector2.Lerp(startVelocity, new Vector2(0, startVelocity.y), timer / momentumDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        // Ensure the velocity is exactly zero at the end.
        rb.velocity = new Vector2(0, rb.velocity.y);
        momentumCoroutine = null; // Signal that the coroutine is finished.
    }
    // --- CORE LOGIC ---
    private void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            isCombatMode = !isCombatMode;
            animator.SetBool(isCombatModeHash, isCombatMode);

            if (!isCombatMode)
            {
                // NEW: Ensure rigidbody is fully dynamic when exiting combat
                rb.isKinematic = false;
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.velocity = Vector2.zero;  // Clear any stuck velocity
                isLockedOn = false;
                lockedOnEnemy = null;
                Debug.Log("<color=yellow>Lock-on disabled - exited combat mode</color>");
            }
            else
            {
                // NEW: When entering combat, keep rigidbody dynamic (not kinematic)
                rb.isKinematic = false;
                rb.bodyType = RigidbodyType2D.Dynamic;
                FindAndLockNearestEnemy();
            }

            SwitchState(isCombatMode ? MovementState.CombatIdle : MovementState.Idle);
        }
    }
    private void FindAndLockNearestEnemy()
    {
        EnemyAI[] allEnemies = FindObjectsOfType<EnemyAI>();

        if (allEnemies.Length == 0)
        {
            Debug.LogWarning("No enemies found in scene!");
            isLockedOn = false;
            return;
        }

        EnemyAI closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        foreach (EnemyAI enemy in allEnemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }

        if (closestEnemy != null)
        {
            lockedOnEnemy = closestEnemy.transform;
            isLockedOn = true;
            Debug.Log($"<color=cyan>LOCKED ON TO: {closestEnemy.name}</color>");
        }
    }

    // NEW: Update lock-on facing direction
    private void UpdateLockOnFacing()
    {
        if (!isLockedOn || lockedOnEnemy == null)
        {
            return;
        }

        // Check if enemy is still alive
        EnemyHealth enemyHealth = lockedOnEnemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null && enemyHealth.isDead)
        {
            // Enemy died, find new target
            FindAndLockNearestEnemy();
            return;
        }

        // Calculate direction to enemy
        float directionToEnemy = lockedOnEnemy.position.x - transform.position.x;

        // Face the enemy
        if (directionToEnemy > 0 && !isFacingRight)
        {
            isFacingRight = true;
            Debug.Log("<color=yellow>Lock-on: Flipped RIGHT</color>");
        }
        else if (directionToEnemy < 0 && isFacingRight)
        {
            isFacingRight = false;
            Debug.Log("<color=yellow>Lock-on: Flipped LEFT</color>");
        }
    }
    private void FlipCharacter()
    {
        // DON'T flip if attacking, being knocked back, or dashing
        if (isBeingKnockedBack || currentState == MovementState.Dashing)
        {
            return;
        }

        // Check if AttackManager says we're attacking
        if (attackManager != null && attackManager.IsAttacking())
        {
            return;
        }

        // Don't flip if dash attack just finished
        if (isDashAttackJustFinished)
        {
            return;
        }

        // NEW: If locked on, use lock-on facing system
        if (isLockedOn && isCombatMode)
        {
            UpdateLockOnFacing();
        }
        // Walk mode: flip based on input
        else if (!isCombatMode)
        {
            if (horizontalInput > 0 && !isFacingRight) isFacingRight = true;
            else if (horizontalInput < 0 && isFacingRight) isFacingRight = false;
        }

        // NEW: APPLY ROTATION AND SCALE TOGETHER IN ONE OPERATION
        ApplyFlip();
    }

    // NEW: Separate method to apply flip - ensures rotation and scale are always together
    private void ApplyFlip()
    {
        // Get the target rotation and scale based on facing direction
        Vector3 targetRotation = isFacingRight ? rightFacingRotation : leftFacingRotation;
        float targetScaleY = isFacingRight ? 1 : -1;

        // APPLY BOTH AT THE SAME TIME
        transform.rotation = Quaternion.Euler(targetRotation);
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), targetScaleY, transform.localScale.z);

        // Apply to particle effects
        if (breathEffect != null)
        {
            breathEffect.transform.localScale = new Vector3(isFacingRight ? 1 : -1, 1, 1);
        }
        if (SmokeEffect != null)
        {
            SmokeEffect.transform.localScale = new Vector3(isFacingRight ? 1 : -1, 1, 1);
        }

        Debug.Log($"<color=cyan>Flip applied: Facing {(isFacingRight ? "RIGHT" : "LEFT")} - Rotation: {targetRotation}, Scale Y: {targetScaleY}</color>");
    }
    public void ForceFlip(Transform target)
    {
        float directionToTarget = target.position.x - transform.position.x;

        if (directionToTarget > 0)
        {
            isFacingRight = true;
        }
        else if (directionToTarget < 0)
        {
            isFacingRight = false;
        }

        // IMMEDIATELY apply the flip
        ApplyFlip();

        Debug.Log($"<color=cyan>Force flip applied - Facing {(isFacingRight ? "RIGHT" : "LEFT")}</color>");
    }
    private void HandleCombatFlip()
    {
        // If facing right, need to double-tap A to flip left
        if (isFacingRight && Input.GetKeyDown(KeyCode.A))
        {
            float timeSinceLastTap = Time.time - lastTapTime_Left;
            if (timeSinceLastTap < doubleTapTimeThreshold)
            {
                // Double-tap detected! Flip!
                isFacingRight = false;
                Debug.Log("<color=yellow>FLIPPED LEFT!</color>");
                lastTapTime_Left = -1f; // Reset to prevent accidental flips
            }
            else
            {
                lastTapTime_Left = Time.time; // Record first tap
            }
        }
        // If facing left, need to double-tap D to flip right
        else if (!isFacingRight && Input.GetKeyDown(KeyCode.D))
        {
            float timeSinceLastTap = Time.time - lastTapTime_Right;
            if (timeSinceLastTap < doubleTapTimeThreshold)
            {
                // Double-tap detected! Flip!
                isFacingRight = true;
                Debug.Log("<color=yellow>FLIPPED RIGHT!</color>");
                lastTapTime_Right = -1f; // Reset to prevent accidental flips
            }
            else
            {
                lastTapTime_Right = Time.time; // Record first tap
            }
        }
    }
    private void Jump()
    {
        // To jump with a kinematic Rigidbody, we need to temporarily make it dynamic.
        StartCoroutine(JumpCoroutine());
    }

    private IEnumerator JumpCoroutine()
    {
        // If we are in combat mode, we need to switch to dynamic for the jump.
        if (isCombatMode)
        {
            rb.isKinematic = false;
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        rb.velocity = new Vector2(rb.velocity.x, 0); // Reset y velocity before jump
        rb.AddForce(new Vector2(0, jumpForce), ForceMode2D.Impulse);
        SwitchState(MovementState.Jumping);

        // NEW: Wait MUCH LONGER for the jump to complete naturally
        // Don't switch back to kinematic until the jump is done
        yield return new WaitForSeconds(0.5f);  // INCREASED from 0.1f

        // NEW: Only switch back to kinematic if we're still in combat mode AND grounded
        if (isCombatMode && isGrounded)
        {
            rb.isKinematic = true;
        }
    }
    public void StopDashAttack()
    {
        // Exit dash state immediately
        if (currentState == MovementState.Dashing)
        {
            SwitchState(MovementState.CombatIdle);
        }

        // NEW: Clear all velocity to prevent sliding
        rb.velocity = Vector2.zero;

        // NEW: Apply slight momentum (optional - remove if you want complete stop)
        // Vector2 momentumDirection = isFacingRight ? Vector2.right : Vector2.left;
        // rb.velocity = momentumDirection * 2f;  // Slight forward momentum

        Debug.Log("<color=cyan>Dash attack stopped - velocity cleared!</color>");
    }



    private void HandleDashInput()
    {
        if (!isCombatMode || !isGrounded || currentState == MovementState.Dashing) return;

        if (Input.GetKeyDown(KeyCode.C))
        {
            PlayerHealth playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth != null && playerHealth.IsDodgeWindowActive() && !dodgeInputConsumed)
            {
                PerformDodge(playerHealth.GetCurrentEnemyAttackType());
                dodgeInputConsumed = true;  // LOCK: Only one dodge per window
                return;
            }
        }


        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
           
            // Determine if dashing forward or backward based on facing direction
            bool isDashingForward = (isFacingRight && horizontalInput >= 0) || (!isFacingRight && horizontalInput <= 0);

            if (horizontalInput < 0 && isFacingRight) // A pressed while facing right = backward dash
            {
                dashDirection = Vector2.left;
                SwitchState(MovementState.Dashing, false); // Backward dash
                Debug.Log("<color=cyan>BACKWARD DASH!</color>");
            }
            else if (horizontalInput > 0 && !isFacingRight) // D pressed while facing left = backward dash
            {
                dashDirection = Vector2.right;
                SwitchState(MovementState.Dashing, false); // Backward dash
                Debug.Log("<color=cyan>BACKWARD DASH!</color>");
            }
            else
            {
                // Forward dash (or no input = forward)
                dashDirection = isFacingRight ? Vector2.right : Vector2.left;
                SwitchState(MovementState.Dashing, true); // Forward dash
                Debug.Log("<color=cyan>FORWARD DASH!</color>");
            }
            return;
        }

        // Double-tap A or D to FLIP (only in combat mode)
        if (Input.GetKeyDown(KeyCode.A))
        {
            float timeSinceLastTap = Time.time - lastTapTime_Left;
            if (timeSinceLastTap < doubleTapTimeThreshold && isFacingRight)
            {
                // Double-tap A while facing right = FLIP LEFT
                isFacingRight = false;
                Debug.Log("<color=yellow>FLIPPED LEFT!</color>");
                lastTapTime_Left = -1f;
            }
            else
            {
                lastTapTime_Left = Time.time;
            }
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            float timeSinceLastTap = Time.time - lastTapTime_Right;
            if (timeSinceLastTap < doubleTapTimeThreshold && !isFacingRight)
            {
                // Double-tap D while facing left = FLIP RIGHT
                isFacingRight = true;
                Debug.Log("<color=yellow>FLIPPED RIGHT!</color>");
                lastTapTime_Right = -1f;
            }
            else
            {
                lastTapTime_Right = Time.time;
            }
        }
    }
    private void UpdateState()
    {
        if (isAttackLocked) return;

        switch (currentState)
        {
            case MovementState.Idle:
                if (Input.GetKeyDown(KeyCode.Space) && isGrounded) Jump();
                else if (horizontalInput != 0 && isGrounded) SwitchState(MovementState.Running);
                else if (!isGrounded && rb.velocity.y < -0.1f) SwitchState(MovementState.Falling);
                break;
            case MovementState.Running:
                if (Input.GetKeyDown(KeyCode.Space) && isGrounded) Jump();
                else if (horizontalInput == 0 && isGrounded) SwitchState(MovementState.Idle);
                else if (!isGrounded && rb.velocity.y < -0.1f) SwitchState(MovementState.Falling);
                break;
            case MovementState.CombatIdle:
                if (Input.GetKeyDown(KeyCode.Space) && isGrounded) Jump();
                else if (horizontalInput != 0 && isGrounded)
                {
                    // Determine if moving forward or backward based on facing direction
                    bool isMovingForward = (isFacingRight && horizontalInput > 0) || (!isFacingRight && horizontalInput < 0);

                    if (isMovingForward)
                    {
                        SwitchState(MovementState.CombatMoveForward);
                    }
                    else
                    {
                        SwitchState(MovementState.CombatMoveBackward);
                    }
                }
                else if (!isGrounded && rb.velocity.y < -0.1f) SwitchState(MovementState.Falling);
                break;
            case MovementState.CombatMoveForward:
            case MovementState.CombatMoveBackward:
                if (Input.GetKeyDown(KeyCode.Space) && isGrounded) Jump();
                else if (horizontalInput == 0 && isGrounded) SwitchState(MovementState.CombatIdle);
                else if (!isGrounded && rb.velocity.y < -0.1f) SwitchState(MovementState.Falling);
                else
                {
                    // Determine if moving forward or backward based on facing direction
                    bool isMovingForward = (isFacingRight && horizontalInput > 0) || (!isFacingRight && horizontalInput < 0);

                    if (isMovingForward && currentState != MovementState.CombatMoveForward)
                    {
                        SwitchState(MovementState.CombatMoveForward);
                    }
                    else if (!isMovingForward && currentState != MovementState.CombatMoveBackward)
                    {
                        SwitchState(MovementState.CombatMoveBackward);
                    }
                }
                break;
            case MovementState.Jumping:
                if (rb.velocity.y < -0.1f) SwitchState(MovementState.Falling);
                break;
            case MovementState.Falling:
                if (isGrounded) SwitchState(MovementState.Landing);
                break;
            case MovementState.Landing:
                // A short delay to let the land animation play before returning to idle.
                StartCoroutine(LandToIdleDelay());
                break;
            case MovementState.Dashing:
                dashTimer -= Time.deltaTime;
                if (dashTimer <= 0) SwitchState(MovementState.CombatIdle);
                break;
        }
    }

    private IEnumerator LandToIdleDelay()
    {
        yield return new WaitForSeconds(0.1f); // Adjust this delay as needed
        SwitchState(isCombatMode ? MovementState.CombatIdle : MovementState.Idle);
    }

    private void SwitchState(MovementState newState, bool isDashForward = true)
    {
        if (newState == currentState && newState != MovementState.Jumping) return; // Allow re-triggering jump
        currentState = newState;
        animator.SetBool(isRunningHash, false);
        animator.SetBool(isFallingHash, false);
        animator.SetBool(isMovingForwardHash, false);
        animator.SetBool(isMovingBackwardHash, false);
        switch (currentState)
        {
            case MovementState.Idle: break;
            case MovementState.Running: animator.SetBool(isRunningHash, true); break;
            case MovementState.CombatIdle: break;
            case MovementState.CombatMoveForward: animator.SetBool(isMovingForwardHash, true); break;
            case MovementState.CombatMoveBackward: animator.SetBool(isMovingBackwardHash, true); break;
            case MovementState.Jumping: animator.SetTrigger(jumpTriggerHash); break;
            case MovementState.Falling: animator.SetBool(isFallingHash, true); break;
            case MovementState.Landing: animator.SetTrigger(landTriggerHash); break;
            case MovementState.Dashing:
                dashTimer = dashDuration; // Set the timer.
                if (isDashForward) animator.SetTrigger(dashForwardTriggerHash);
                else animator.SetTrigger(dashBackwardTriggerHash);
                break;
        }
    }
    public void TriggerGuardBreakKnockback(Transform source, float distance, float duration)
    {
        StartCoroutine(GuardBreakKnockbackCoroutine(source, distance, duration));
    }

    private IEnumerator GuardBreakKnockbackCoroutine(Transform source, float distance, float duration)
    {
        // 1. Set the master lock.
        isBeingKnockedBack = true;
        rb.velocity = Vector2.zero; // Stop all physics momentum.
        rb.isKinematic = true;      // Temporarily ignore physics forces like gravity.

        // 2. Calculate the positions (this part is correct).
        Vector3 startPosition = transform.position;
        Vector2 knockbackDirection = new Vector2(transform.position.x - source.position.x, 0).normalized;
        Vector3 endPosition = startPosition + (Vector3)knockbackDirection * distance;

        // 3. The smooth movement loop.
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            // This is an "EaseInOut" function. It starts slow, speeds up, and ends slow.
            // It feels much more natural and less "buggy" than a linear Lerp.
            float t = elapsedTime / duration;
            t = t * t * (3f - 2f * t); // This is a SmoothStep function.

            transform.position = Vector3.Lerp(startPosition, endPosition, t);

            elapsedTime += Time.deltaTime;
            yield return null; // Wait for the next frame.
        }

        // 4. Clean up and release the locks.
        transform.position = endPosition; // Ensure final position is exact.
        rb.isKinematic = false;       // Re-enable physics.
        isBeingKnockedBack = false;   // Release the master lock.
    }
    public void ApplyKnockback(Transform source, float distance, float duration)
    {
        // We can reuse the GetParriedKnockbackCoroutine, as it does exactly what we need!
        StartCoroutine(KnockbackCoroutine(source, distance, duration));
    }

    // Let's rename GetParriedKnockbackCoroutine to be more generic.
    // Find and replace "GetParriedKnockbackCoroutine" with "KnockbackCoroutine"
    private IEnumerator KnockbackCoroutine(Transform source, float distance, float duration)
    {
        isAttackLocked = true;
        rb.velocity = Vector2.zero;

        Vector2 rawDirection = transform.position - source.position;
        Vector2 knockbackDirection = new Vector2(rawDirection.x, 0).normalized;

        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + (Vector3)knockbackDirection * distance;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            // --- THIS IS THE GUARANTEED FIX FOR SMOOTHNESS ---
            // 1. Calculate our progress through the duration (a value from 0 to 1).
            float progress = elapsedTime / duration;

            // 2. Use the Animation Curve to get the "eased" progress.
            //    This is the magic part. The curve remaps the linear progress to a smooth curve.
            float curveValue = knockbackCurve.Evaluate(progress);

            // 3. Use Vector3.Lerp, but pass it the NEW, curved progress value.
            transform.position = Vector3.Lerp(startPosition, endPosition, curveValue);
            // --- END OF FIX ---

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = endPosition;
        rb.velocity = Vector2.zero;
        isAttackLocked = false;
    }

    // Now, update your existing GetParried method to use the new system.
    public void GetParried(Transform enemyTransform)
    {
        // It now calls the generic coroutine with its specific values.
        StartCoroutine(KnockbackCoroutine(enemyTransform, getParriedKnockbackDistance, getParriedKnockbackDuration));
    }
    public bool IsDashing()
    {
        return currentState == MovementState.Dashing;
    }
    public bool IsGrounded()
    {
        return isGrounded;
    }
    public void FlipTowards(Transform target)
    {
        float directionToTarget = target.position.x - transform.position.x;
        if (directionToTarget > 0 && !isFacingRight)
        {
            isFacingRight = true;
        }
        else if (directionToTarget < 0 && isFacingRight)
        {
            isFacingRight = false;
        }
        // Apply the rotation immediately.
        transform.rotation = Quaternion.Euler(isFacingRight ? rightFacingRotation : leftFacingRotation);
    }

    private void PerformDodge(string attackType)
    {
        Debug.Log($"<color=cyan>DODGE PERFORMED! Attack type: {attackType}</color>");

        string dodgeAnimationName = "";

        if (attackType == "lightAttack1") dodgeAnimationName = "dodge1";
        else if (attackType == "lightAttack2") dodgeAnimationName = "dodge2";
        else if (attackType == "lightAttack3") dodgeAnimationName = "dodge3";
        else if (attackType == "attack1") dodgeAnimationName = "dodge1";
        else if (attackType == "attack2") dodgeAnimationName = "dodge2";

        Debug.Log($"<color=cyan>Dodge animation name: {dodgeAnimationName}</color>");

        int dodgeHash = Animator.StringToHash(dodgeAnimationName);
        Debug.Log($"<color=cyan>Dodge hash: {dodgeHash}</color>");

        animator.SetTrigger(dodgeHash);
        Debug.Log($"<color=cyan>Trigger set!</color>");

        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            // REMOVED: SetDodgeActive(true) - now controlled by animation events
            StartCoroutine(playerHealth.SlowMoEffect());
        }

        StartCoroutine(EndDodgeCoroutine());
    }

    private IEnumerator EndDodgeCoroutine()
    {
        yield return new WaitForSeconds(0.5f); // Adjust to dodge animation length

        // REMOVED: SetDodgeActive(false) - now controlled by animation events
        dodgeInputConsumed = false;
        Debug.Log("<color=yellow>Dodge ended - ready for next dodge</color>");
    }
    public void ResetDodgeInput()
    {
        dodgeInputConsumed = false;
        Debug.Log("<color=cyan>Dodge input reset - ready for next dodge!</color>");
    }
    public bool IsDashingForward()
    {
        if (currentState != MovementState.Dashing)
            return false;

        // Check if dash direction matches facing direction (forward dash)
        bool isForwardDash = (isFacingRight && dashDirection == Vector2.right) ||
                             (!isFacingRight && dashDirection == Vector2.left);
        return isForwardDash;
    }
    public void SetDashAttackJustFinished(bool finished)
    {
        isDashAttackJustFinished = finished;
        if (finished)
        {
            Debug.Log("<color=yellow>Dash attack flip lock ENABLED</color>");
        }
        else
        {
            Debug.Log("<color=yellow>Dash attack flip lock DISABLED</color>");
        }
    }
    public Transform GetLockedOnEnemy()
    {
        if (isLockedOn && lockedOnEnemy != null)
        {
            return lockedOnEnemy;
        }
        return null;
    }


    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
    }
}
