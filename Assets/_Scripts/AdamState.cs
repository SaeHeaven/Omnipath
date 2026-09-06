using UnityEngine;
using System;

public class AdamState : MonoBehaviour
{
    public static AdamState Instance { get; private set; }
    public event Action OnStateChanged;

    [Header("Health Pools")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaRegenRate = 12f;
    public float regenDelayDuration = 0.3f;
    private float regenDelayTimer = 0f;

    public bool isExhausted { get; private set; } = false;
    public float exhaustionRecoveryThreshold = 25f;

    [Header("Defense State")]
    public bool isBlocking = false;

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
        currentStamina = maxStamina;
        currentAmmo = maxAmmo;
    }

    private void Start()
    {
        OnStateChanged?.Invoke();
    }

    private void Update()
    {
        if (regenDelayTimer > 0f)
        {
            regenDelayTimer -= Time.deltaTime;
        }
        else if (currentStamina < maxStamina)
        {
            currentStamina = Mathf.Clamp(currentStamina + staminaRegenRate * Time.deltaTime, 0f, maxStamina);

            if (isExhausted && currentStamina >= exhaustionRecoveryThreshold)
            {
                isExhausted = false;
                Debug.Log("  EXHAUSTION BROKEN: Movement and actions restored.");
            }
            OnStateChanged?.Invoke();
        }
    }

    public bool ConsumeStamina(float amount)
    {
        if (isExhausted || currentStamina < amount)
        {
            return false;
        }
        currentStamina = Mathf.Clamp(currentStamina - amount, 0f, maxStamina);
        regenDelayTimer = regenDelayDuration;
        if (currentStamina <= 0f)
        {
            isExhausted = true;
            Debug.LogWarning("  STAMINA FLATLINE: Adam entered Exhaustion State!");
        }
        OnStateChanged?.Invoke();
        return true;
    }

    public void RefundStamina(float amount)
    {
        currentStamina = Mathf.Clamp(currentStamina + amount, 0f, maxStamina);
        if (isExhausted && currentStamina >= exhaustionRecoveryThreshold)
        {
            isExhausted = false;
        }
        OnStateChanged?.Invoke();
    }

    public void TakeDamage(float amount, Vector3 knockbackDir)
    {
        // Trigger directional camera recoil
        if (AdamCameraImpact.Instance != null)
        {
            AdamCameraImpact.Instance.TriggerHitImpact(knockbackDir, amount, maxHealth);
        }
        if (isBlocking)
        {
            float healthDamage = amount * 0.25f;  // Take 25% of original damage (75% reduction)
            float staminaDamage = amount * 0.75f; // Take 75% of original damage to stamina

            currentHealth = Mathf.Clamp(currentHealth - healthDamage, 0f, maxHealth);
            currentStamina = Mathf.Clamp(currentStamina - staminaDamage, 0f, maxStamina);

            regenDelayTimer = regenDelayDuration; // Pause stamina regen when hit while blocking
            // Trigger animation restart on block hit
            if (AdamMelee.Instance != null)
            {
                AdamMelee.Instance.OnBlockHit();
            }
            if (currentStamina <= 0f)
            {
                isExhausted = true;
                isBlocking = false; // Guard broken upon stamina depletion
                Debug.LogWarning("  GUARD BROKEN! Adam ran out of stamina while blocking!");
            }

            Debug.Log($"  BLOCKED HIT! Reduced HP Damage: {healthDamage:F1} | Stamina Loss: -{staminaDamage:F1}");
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        }

        OnStateChanged?.Invoke();
    }

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