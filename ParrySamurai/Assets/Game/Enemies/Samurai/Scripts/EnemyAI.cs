// EnemyAI.cs (FINAL - With Brain Lock & Trigger Reset)

using System.Collections;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Parry Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float parryChance = 0.5f;

    [Header("Components")]
    private Animator animator;
    private EnemyHealth healthScript;

    private readonly int parryTriggerHash = Animator.StringToHash("parry");
    private readonly int counterAttackTriggerHash = Animator.StringToHash("counterAttack");

   

    // --- THIS IS THE NEW "BRAIN LOCK" ---
    private bool isLocked = false;
    [Header("Counter Attack Settings")]
    [SerializeField] private EnemyAttack enemyAttackScript;
    [Tooltip("How long to wait after the warning before lunging.")]
    [SerializeField] private float counterWarningDelay = 0.6f;
    [SerializeField] private ParticleSystem counterWarningGlint;
    private bool isReadyToParry = false;
   

    void Awake()
    {
        animator = GetComponent<Animator>();
        healthScript = GetComponent<EnemyHealth>();
    }

    public void OnParryDecision()
    {
        if (!isReadyToParry) return;

        float roll = Random.Range(0f, 1f);
        if (roll <= parryChance)
        {
            // --- THIS IS THE GUARANTEED FIX ---
            // 1. DECISION: PARRY!
            Debug.Log("<color=cyan>ENEMY AI: Decision is PARRY. Forcing state now.</color>");

            // 2. IMMEDIATELY tell the health script to enter the parry state.
            if (healthScript != null)
            {
                healthScript.StartParryState(); // We will create this new method.
            }

            // 3. Trigger the animation.
            animator.SetTrigger(parryTriggerHash);
            // --- END OF FIX ---
        }

        isReadyToParry = false;
    }


    public void ForceCounterAttack()
    {
        Debug.Log("<color=red>ENEMY AI: Received ForceCounterAttack command! Starting sequence.</color>");
        // We don't check for a counter here, we just start the coroutine.
        StartCoroutine(CounterAttackSequence());
    }

    private IEnumerator CounterAttackSequence()
    {
        // --- The warning glint logic is perfect ---
        if (counterWarningGlint != null)
        {
            counterWarningGlint.gameObject.SetActive(true);
            counterWarningGlint.Play();
        }

        // Wait for the delay.
        yield return new WaitForSeconds(counterWarningDelay);

        // Fire the trigger for the actual counter-attack animation.
        Debug.Log("Warning finished. Firing 'counterAttack' trigger!");
        animator.SetTrigger(counterAttackTriggerHash);

        // --- The logic to disable the glint is also perfect ---
        if (counterWarningGlint != null)
        {
            yield return new WaitForSeconds(1f);
            counterWarningGlint.gameObject.SetActive(false);
        }
    }
    public void PrepareForPlayerAttack()
    {
        // This is called by the player's animation event.
        // It just flips a switch to say "I'm ready."
        isReadyToParry = true;
        if (healthScript != null && healthScript.IsStunned())
        {
            Debug.Log("<color=red>ENEMY AI: Aborting PrepareForAttack because I am stunned!</color>");
            return;
        }
        // Start a coroutine to automatically turn the switch off after a moment.
        // This prevents the enemy from being "ready" forever.
        StartCoroutine(ParryReadinessWindow());
    }

    private IEnumerator ParryReadinessWindow()
    {
        // Wait for a short time (e.g., half a second). This is the window
        // during which the enemy is actively looking for a parry.
        yield return new WaitForSeconds(0.5f);
        isReadyToParry = false;
    }
   
}
