using UnityEngine;
using System; 

public class AdamState : MonoBehaviour
{
    public static AdamState Instance { get; private set; }

    // This is the "Shout" event that the UI will listen for
    public event Action OnStateChanged; 

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
        currentAmmo = maxAmmo;
    }

    private void Start()
    {
        // Shout once at the start so the UI sets itself up instantly
        OnStateChanged?.Invoke(); 
    }

    public void GainHatred(float amount)
    {
        currentHatred = Mathf.Clamp(currentHatred + amount, 0f, maxHatred);
        OnStateChanged?.Invoke(); 
    }

    public bool SpendHatred(float amount)
    {
        if (currentHatred >= amount)
        {
            currentHatred -= amount;
            OnStateChanged?.Invoke(); 
            return true; 
        }
        return false; 
    }

    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        OnStateChanged?.Invoke(); 
    }

    // --- NEW AMMO METHODS ---
    public void ConsumeAmmo()
    {
        currentAmmo--;
        OnStateChanged?.Invoke();
    }

    public void ReloadAmmo()
    {
        currentAmmo = maxAmmo;
        OnStateChanged?.Invoke();
    }
}