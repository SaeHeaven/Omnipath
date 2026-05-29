using UnityEngine;

public class AdamState : MonoBehaviour
{
    public static AdamState Instance { get; private set; }

    [Header("Health Pools")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Hatred Resource")]
    public float maxHatred = 100f;
    public float currentHatred;

    [Header("Firearm Ammunition")]
    public int maxAmmo = 8;
    public int currentAmmo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        currentHealth = maxHealth;
        currentHatred = 0f;
        currentAmmo = maxAmmo; // Start with a loaded gun
    }

    public void GainHatred(float amount)
    {
        currentHatred = Mathf.Clamp(currentHatred + amount, 0f, maxHatred);
    }

    // --- NEW METHOD: Consumes Hatred to reload/fire weapon ---
    public bool SpendHatred(float amount)
    {
        if (currentHatred >= amount)
        {
            currentHatred -= amount;
            return true; // Successfully spent!
        }
        return false; // Not enough Hatred!
    }

    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
    }
}