// AttackManager.cs (Seamless Combo Version)

using UnityEngine;

public class AttackManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private ZreyMovements playerMovement;

    [Header("Effects")]
    [SerializeField] private ParticleSystem slashEffect1;
    [SerializeField] private ParticleSystem slashEffect2;

    [Header("Combo Settings")]
    private int comboStep = 0;
    private bool isAttacking = false;
    // ---- NEW ----: This is our simple "input buffer".
    private bool attackQueued = false;

    // --- Animation Hashes ---
    private readonly int attack1TriggerHash = Animator.StringToHash("attack1");
    private readonly int attack2TriggerHash = Animator.StringToHash("attack2");

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (playerMovement == null) playerMovement = GetComponent<ZreyMovements>();
    }

    void Update()
    {
        // We check for input every single frame, no matter what.
        if (Input.GetMouseButtonDown(0))
        {
            // If we click, we queue an attack.
            attackQueued = true;
        }

        // Now, we decide what to do with that queued attack.
        HandleAttacks();
    }

    private void HandleAttacks()
    {
        // If no attack was queued this frame, do nothing.
        if (!attackQueued) return;

        // If we are in the middle of an attack...
        if (isAttacking)
        {
            // ...and we are ready for the second hit, perform Attack 2.
            if (comboStep == 1)
            {
                PerformAttack(2);
            }
        }
        // If we are NOT attacking, it means we are free to start a new combo.
        else
        {
            PerformAttack(1);
        }

        // We've processed the click, so we reset the queue for the next frame.
        attackQueued = false;
    }

    private void PerformAttack(int step)
    {
        // This check is still useful to prevent weird state overlaps.
        if (!isAttacking && step > 1) return;

        isAttacking = true;
        playerMovement.SetAttacking(true);

        if (step == 1)
        {
            animator.SetTrigger(attack1TriggerHash);
            comboStep = 1;
        }
        else if (step == 2)
        {
            animator.SetTrigger(attack2TriggerHash);
            comboStep = 2;
        }
    }

    // --- ANIMATION EVENT METHODS ---

    // This method is now CRITICAL. It's what allows the next attack to happen instantly.
    /// <summary>
    /// Called by an Animation Event at the end of any attack animation.
    /// </summary>
    public void FinishAttack()
    {
        isAttacking = false;
        playerMovement.SetAttacking(false);
        comboStep = 0;

        // ---- THE SECRET SAUCE ----
        // After finishing an attack, we immediately check if another attack was queued.
        // If the player was spamming the click, this will be true.
        if (attackQueued)
        {
            // If an attack was queued, we immediately start the next combo.
            // This happens in the SAME FRAME that the old attack ended.
            HandleAttacks();
        }
    }

    // The rest of the event methods are the same.
    public void TriggerSlashEffect1()
    {
        if (slashEffect1 != null) slashEffect1.Play();
    }

    public void TriggerSlashEffect2()
    {
        if (slashEffect2 != null) slashEffect2.Play();
    }
}
