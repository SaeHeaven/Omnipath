using UnityEngine;
using UnityEngine.UI;
using TMPro; // Library addition to manipulate TextMeshPro text strings

public class AdamHUD : MonoBehaviour
{
    public Slider healthSlider;
    public Slider hatredSlider;
    public TextMeshProUGUI ammoTextDisplay; // Core reference slot

    private void Start()
    {
        if (AdamState.Instance != null)
        {
            healthSlider.maxValue = AdamState.Instance.maxHealth;
            hatredSlider.maxValue = AdamState.Instance.maxHatred;
        }
    }

    private void Update()
    {
        if (AdamState.Instance != null)
        {
            healthSlider.value = AdamState.Instance.currentHealth;
            hatredSlider.value = AdamState.Instance.currentHatred;
            
            // Format our text readouts cleanly to track current ammunition
            ammoTextDisplay.text = "AMMO: " + AdamState.Instance.currentAmmo.ToString("00");
        }
    }
}