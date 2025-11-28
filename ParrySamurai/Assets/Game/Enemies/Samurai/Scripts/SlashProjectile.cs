// In SlashProjectile.cs
using FirstGearGames.SmoothCameraShaker;
using UnityEngine;

public class SlashProjectile : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private int damage = 10;

    private Rigidbody2D rb;
    private bool hasBeenParried = false;
    public ShakeData CameraShakeParry;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Destroy the object after a safe amount of time if it hits nothing.
        Destroy(gameObject, 5f);
    }

    public void Launch(Vector2 direction)
    {
        Vector2 worldMoveDirection = new Vector2(Mathf.Sign(direction.x), 0);
        rb.velocity = worldMoveDirection * speed;

        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play(true);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // If the projectile has already been parried, it can no longer deal damage.
        if (hasBeenParried) return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                // --- THIS IS THE GUARANTEED "GO THROUGH" FIX ---
                // We pass a reference to THIS projectile script to the TakeDamage method.
                playerHealth.TakeDamage(damage, null, this);
                CameraShakerHandler.Shake(CameraShakeParry);
                // We DO NOT destroy the projectile here. It will continue flying.
                // --- END OF FIX ---
            }
        }
    }

    // --- THIS IS THE NEW "PARRY STOP" METHOD ---
    /// <summary>
    /// This public method is called by the PlayerHealth script upon a successful parry.
    /// </summary>
    public void OnParried()
    {
        Debug.Log("Projectile has been parried! Fading out.");
        hasBeenParried = true;

        // Stop the projectile's movement.
        rb.velocity = Vector2.zero;

        // --- THIS IS THE GUARANTEED "FADE OUT" FIX ---
        // Find all particle systems on this object and its children.
        ParticleSystem[] allParticles = GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in allParticles)
        {
            // Tell each particle system to stop emitting new particles.
            // The existing particles will live out their life and fade away naturally.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        // --- END OF FIX ---

        // Destroy the parent GameObject after a delay to allow particles to fade.
        Destroy(gameObject, 3f); // Adjust this time based on your particle lifetime.
    }
}
