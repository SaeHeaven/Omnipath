using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AdamHUD : MonoBehaviour
{
    public Slider healthSlider;
    public Slider staminaSlider; // Updated from hatredSlider
    public TextMeshProUGUI ammoTextDisplay;

    private void Start()
    {
        if (AdamState.Instance != null)
        {
            healthSlider.maxValue = AdamState.Instance.maxHealth;
            staminaSlider.maxValue = AdamState.Instance.maxStamina;
            
            AdamState.Instance.OnStateChanged += UpdateHUD;
            UpdateHUD();
        }
    }

    private void OnDestroy()
    {
        if (AdamState.Instance != null)
        {
            AdamState.Instance.OnStateChanged -= UpdateHUD;
        }
    }

    private void UpdateHUD()
    {
        healthSlider.value = AdamState.Instance.currentHealth;
        staminaSlider.value = AdamState.Instance.currentStamina;
        ammoTextDisplay.text = "AMMO: " + AdamState.Instance.currentAmmo.ToString("00");
    }
}