// ZreyMovements.cs (FINAL - Works WITH Apply Root Motion)

using UnityEngine;
using System.Collections;

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
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        playerHealth = GetComponent<PlayerHealth>();
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
            // MovePosition is the guaranteed way to move a Dynamic Rigidbody
            // while respecting physics collisions.
            rb.MovePosition(rb.position + dashDirection * dashSpeed * Time.fixedDeltaTime);
            return; // Stop here to ensure nothing else interferes.
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

        // Apply movement based on the current mode.
        if (isCombatMode)
        {
            if (canCombatMove)
            {
                // In combat, move forward/backward based on input.
                float moveDirection = isFacingRight ? horizontalInput : -horizontalInput;
                float targetSpeed = moveDirection > 0 ? combatMoveSpeed : combatMoveSpeed * 0.8f; // Optional: move slightly slower backwards
                rb.velocity = new Vector2(horizontalInput * targetSpeed, rb.velocity.y);
            }
            else
            {
                // If the gate is closed, we are not moving.
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
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            isCombatMode = !isCombatMode;
            animator.SetBool(isCombatModeHash, isCombatMode);

           
            if (!isCombatMode)
            {
                rb.bodyType = RigidbodyType2D.Dynamic; // Ensure it's fully dynamic for walk mode
            }

            SwitchState(isCombatMode ? MovementState.CombatIdle : MovementState.Idle);
        }
    }

    private void FlipCharacter()
    {
        // This logic is now simple and runs every frame, making it foolproof.
        if (!isCombatMode) // Only flip based on input in Walk Mode
        {
            if (horizontalInput > 0 && !isFacingRight) isFacingRight = true;
            else if (horizontalInput < 0 && isFacingRight) isFacingRight = false;
        }

        // Apply the rotation and scale based on the final 'isFacingRight' state.
        transform.rotation = Quaternion.Euler(isFacingRight ? rightFacingRotation : leftFacingRotation);
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), isFacingRight ? 1 : -1, transform.localScale.z);
        if (breathEffect != null)
        {
            breathEffect.transform.localScale = new Vector3(isFacingRight ? 1 : -1, 1, 1);
        }
        if (SmokeEffect != null)
        {
           SmokeEffect.transform.localScale = new Vector3(isFacingRight ? 1 : -1, 1, 1);
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

        // Wait a moment for the jump to be in the air.
        yield return new WaitForSeconds(0.1f);

        // After the jump, if we are supposed to be in combat mode, return to kinematic.
        if (isCombatMode)
        {
            rb.isKinematic = true;
        }
    }

    private void HandleDashInput()
    {
        if (!isCombatMode || !isGrounded || currentState == MovementState.Dashing || isAttackLocked) return;

        if ((isFacingRight && Input.GetKeyDown(KeyCode.D)) || (!isFacingRight && Input.GetKeyDown(KeyCode.A)))
        {
            float lastTapTime = isFacingRight ? lastTapTime_Right : lastTapTime_Left;
            if (Time.time - lastTapTime < doubleTapTimeThreshold)
            {
                dashDirection = isFacingRight ? Vector2.right : Vector2.left;
                SwitchState(MovementState.Dashing, true);
            }
            if (isFacingRight) lastTapTime_Right = Time.time; else lastTapTime_Left = Time.time;
        }
        else if ((isFacingRight && Input.GetKeyDown(KeyCode.A)) || (!isFacingRight && Input.GetKeyDown(KeyCode.D)))
        {
            float lastTapTime = isFacingRight ? lastTapTime_Left : lastTapTime_Right;
            if (Time.time - lastTapTime < doubleTapTimeThreshold)
            {
                dashDirection = isFacingRight ? Vector2.left : Vector2.right;
                SwitchState(MovementState.Dashing, false);
            }
            if (isFacingRight) lastTapTime_Left = Time.time; else lastTapTime_Right = Time.time;
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
                    float moveDirection = isFacingRight ? horizontalInput : -horizontalInput;
                    if (moveDirection > 0) SwitchState(MovementState.CombatMoveForward);
                    else SwitchState(MovementState.CombatMoveBackward);
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
                    float moveDirection = isFacingRight ? horizontalInput : -horizontalInput;
                    if (moveDirection > 0 && currentState != MovementState.CombatMoveForward) SwitchState(MovementState.CombatMoveForward);
                    else if (moveDirection < 0 && currentState != MovementState.CombatMoveBackward) SwitchState(MovementState.CombatMoveBackward);
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
    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
    }
}
