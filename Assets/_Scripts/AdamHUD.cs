using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class AdamHUD : MonoBehaviour
{
    public Slider healthSlider;
    public Slider hatredSlider;
    public TextMeshProUGUI ammoTextDisplay; 

    private void Start()
    {
        if (AdamState.Instance != null)
        {
            healthSlider.maxValue = AdamState.Instance.maxHealth;
            hatredSlider.maxValue = AdamState.Instance.maxHatred;
            
            // Tell the HUD to listen for the shout
            AdamState.Instance.OnStateChanged += UpdateHUD;
            UpdateHUD(); 
        }
    }

    private void OnDestroy()
    {
        // Clean up the listener when the level ends so the game doesn't crash
        if (AdamState.Instance != null)
        {
            AdamState.Instance.OnStateChanged -= UpdateHUD;
        }
    }

    // This ONLY runs when a number actually changes!
    private void UpdateHUD()
    {
        healthSlider.value = AdamState.Instance.currentHealth;
        hatredSlider.value = AdamState.Instance.currentHatred;
        ammoTextDisplay.text = "AMMO: " + AdamState.Instance.currentAmmo.ToString("00");
    }
}