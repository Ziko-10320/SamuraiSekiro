// ZreyMovements.cs (Corrected Physics Loop)

using UnityEngine;

public class ZreyMovements : MonoBehaviour
{
    private enum MovementState { Idle, Running, Jumping, Falling, Landing, CombatIdle, CombatMoveForward, CombatMoveBackward, Dashing }
    private MovementState currentState;

    [Header("Components")]
    private Rigidbody2D rb;
    private Animator animator;
    [SerializeField] private ParticleSystem breathEffect;
    [Header("Walk Mode Settings")]
    [SerializeField] private float walkMoveSpeed = 8f;

    [Header("Combat Mode Settings")]
    [SerializeField] private float combatMoveSpeed = 5f;
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

    // --- Animation Hashes ---
    private readonly int isRunningHash = Animator.StringToHash("isRunning");
    private readonly int jumpTriggerHash = Animator.StringToHash("jump");
    private readonly int isFallingHash = Animator.StringToHash("isFalling");
    private readonly int landTriggerHash = Animator.StringToHash("land");
    private readonly int isCombatModeHash = Animator.StringToHash("isCombatMode");
    private readonly int isMovingForwardHash = Animator.StringToHash("isMovingForward");
    private readonly int isMovingBackwardHash = Animator.StringToHash("isMovingBackward");
    private readonly int dashForwardTriggerHash = Animator.StringToHash("dashForward");
    private readonly int dashBackwardTriggerHash = Animator.StringToHash("dashBackward");

    private bool isAttackLocked = false;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Update is now ONLY for reading input and managing states/animations. NO physics.
        horizontalInput = Input.GetAxisRaw("Horizontal");
        isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);

        HandleModeSwitch();
        HandleDashInput();
        UpdateState();

        if (!isCombatMode)
        {
            FlipCharacter();
        }
    }

    // ---- MODIFIED & CORRECTED ----
    // All Rigidbody.velocity changes are now inside FixedUpdate.
    void FixedUpdate()
    {
        if (isAttackLocked)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
            return; // Stop here
        }
        // We check the state first.
        if (currentState == MovementState.Dashing)
        {
            // If we are dashing, apply the dash velocity.
            rb.velocity = dashDirection * dashSpeed;
        }
        else
        {
            // If we are NOT dashing, apply normal movement velocity.
            float currentSpeed = isCombatMode ? combatMoveSpeed : walkMoveSpeed;
            rb.velocity = new Vector2(horizontalInput * currentSpeed, rb.velocity.y);
        }
    }

    private void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            isCombatMode = !isCombatMode;
            animator.SetBool(isCombatModeHash, isCombatMode);
            SwitchState(isCombatMode ? MovementState.CombatIdle : MovementState.Idle);
        }
    }

    private void HandleDashInput()
    {
        // Failsafe: Only allow dashing if in combat, on the ground, and not already dashing.
        if (!isCombatMode || !isGrounded || currentState == MovementState.Dashing) return;

        // --- FORWARD DASH LOGIC ---
        // A "forward" dash is when you double-tap in the direction you are currently facing.
        if ((isFacingRight && Input.GetKeyDown(KeyCode.D)) || (!isFacingRight && Input.GetKeyDown(KeyCode.A)))
        {
            // Check for double tap
            float lastTapTime = isFacingRight ? lastTapTime_Right : lastTapTime_Left;
            if (Time.time - lastTapTime < doubleTapTimeThreshold)
            {
                // Set dash direction to be forward (relative to character)
                dashDirection = isFacingRight ? Vector2.right : Vector2.left;
                SwitchState(MovementState.Dashing, true); // 'true' means isDashForward
            }
            // Update the tap time for the correct key
            if (isFacingRight) lastTapTime_Right = Time.time;
            else lastTapTime_Left = Time.time;
        }
        // --- BACKWARD DASH LOGIC ---
        // A "backward" dash is when you double-tap in the opposite direction you are facing.
        else if ((isFacingRight && Input.GetKeyDown(KeyCode.A)) || (!isFacingRight && Input.GetKeyDown(KeyCode.D)))
        {
            // Check for double tap
            float lastTapTime = isFacingRight ? lastTapTime_Left : lastTapTime_Right;
            if (Time.time - lastTapTime < doubleTapTimeThreshold)
            {
                // Set dash direction to be backward (relative to character)
                dashDirection = isFacingRight ? Vector2.left : Vector2.right;
                SwitchState(MovementState.Dashing, false); // 'false' means isDashForward is false (it's a backdash)
            }
            // Update the tap time for the correct key
            if (isFacingRight) lastTapTime_Left = Time.time;
            else lastTapTime_Right = Time.time;
        }
    }

    private void UpdateState()
    {
        switch (currentState)
        {
            case MovementState.Idle:
                if (Input.GetKeyDown(KeyCode.Space) && isGrounded) Jump();
                else if (horizontalInput != 0 && isGrounded) SwitchState(MovementState.Running);
                else if (!isGrounded && rb.velocity.y < 0) SwitchState(MovementState.Falling);
                break;

            case MovementState.Running:
                if (Input.GetKeyDown(KeyCode.Space) && isGrounded) Jump();
                else if (horizontalInput == 0 && isGrounded) SwitchState(MovementState.Idle);
                else if (!isGrounded && rb.velocity.y < 0) SwitchState(MovementState.Falling);
                break;

            case MovementState.CombatIdle:
                if (Input.GetKeyDown(KeyCode.Space) && isGrounded) Jump();
                else if (horizontalInput != 0 && isGrounded)
                {
                    float moveDirection = isFacingRight ? horizontalInput : -horizontalInput;
                    if (moveDirection > 0) SwitchState(MovementState.CombatMoveForward);
                    else SwitchState(MovementState.CombatMoveBackward);
                }
                else if (!isGrounded && rb.velocity.y < 0) SwitchState(MovementState.Falling);
                break;

            case MovementState.CombatMoveForward:
            case MovementState.CombatMoveBackward:
                if (Input.GetKeyDown(KeyCode.Space) && isGrounded) Jump();
                else if (horizontalInput == 0 && isGrounded) SwitchState(MovementState.CombatIdle);
                else if (!isGrounded && rb.velocity.y < 0) SwitchState(MovementState.Falling);
                else
                {
                    float moveDirection = isFacingRight ? horizontalInput : -horizontalInput;
                    if (moveDirection > 0 && currentState != MovementState.CombatMoveForward) SwitchState(MovementState.CombatMoveForward);
                    else if (moveDirection < 0 && currentState != MovementState.CombatMoveBackward) SwitchState(MovementState.CombatMoveBackward);
                }
                break;

            case MovementState.Jumping:
                if (rb.velocity.y < 0) SwitchState(MovementState.Falling);
                break;

            case MovementState.Falling:
                if (isGrounded) SwitchState(MovementState.Landing);
                break;

            case MovementState.Landing:
                if (animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") || animator.GetCurrentAnimatorStateInfo(0).IsName("CombatIdle"))
                {
                    SwitchState(isCombatMode ? MovementState.CombatIdle : MovementState.Idle);
                }
                break;

            case MovementState.Dashing:
                // Dashing state now only counts down the timer. The movement is in FixedUpdate.
                dashTimer -= Time.deltaTime;
                if (dashTimer <= 0)
                {
                    // We don't need to set velocity to zero here anymore, FixedUpdate will take over.
                    SwitchState(MovementState.CombatIdle);
                }
                break;
        }
    }

    private void SwitchState(MovementState newState, bool isDashForward = true)
    {
        if (newState == currentState) return;

        currentState = newState;

        animator.SetBool(isRunningHash, false);
        animator.SetBool(isFallingHash, false);
        animator.SetBool(isMovingForwardHash, false);
        animator.SetBool(isMovingBackwardHash, false);

        switch (currentState)
        {
            case MovementState.Idle: break;
            case MovementState.Running:
                animator.SetBool(isRunningHash, true);
                break;
            case MovementState.CombatIdle: break;
            case MovementState.CombatMoveForward:
                animator.SetBool(isMovingForwardHash, true);
                break;
            case MovementState.CombatMoveBackward:
                animator.SetBool(isMovingBackwardHash, true);
                break;
            case MovementState.Jumping:
                animator.SetTrigger(jumpTriggerHash);
                break;
            case MovementState.Falling:
                animator.SetBool(isFallingHash, true);
                break;
            case MovementState.Landing:
                animator.SetTrigger(landTriggerHash);
                break;
            case MovementState.Dashing:
                // ---- MODIFIED ----
                // We ONLY set the timer and trigger the animation here. NO physics.
                dashTimer = dashDuration;
                if (isDashForward) animator.SetTrigger(dashForwardTriggerHash);
                else animator.SetTrigger(dashBackwardTriggerHash);
                break;
        }
    }

    private void Jump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        SwitchState(MovementState.Jumping);
    }

    private void FlipCharacter()
    {
        if (horizontalInput > 0 && !isFacingRight)
        {
            transform.rotation = Quaternion.Euler(rightFacingRotation);
            transform.localScale = new Vector3(transform.localScale.x, 1, transform.localScale.z);
            isFacingRight = true;
            if (breathEffect != null)
            {
                // Set the particle system's local X scale to 1 to face right.
                breathEffect.transform.localScale = new Vector3(1, 1, 1);
            }
        }
        else if (horizontalInput < 0 && isFacingRight)
        {
            transform.rotation = Quaternion.Euler(leftFacingRotation);
            transform.localScale = new Vector3(transform.localScale.x, -1, transform.localScale.z);
            isFacingRight = false;
            if (breathEffect != null)
            {
                // Set the particle system's local X scale to -1 to face left.
                breathEffect.transform.localScale = new Vector3(-1, 1, 1);
            }
        }
    }
    public void SetAttacking(bool attacking)
    {
        isAttackLocked = attacking;
    }
    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
    }
}
