using UnityEngine;

public class AdamFirearm : MonoBehaviour
{
    private PlayerControls controls;
    private Transform cameraTransform;

    [Header("Ballistic Settings")]
    public float fireRange = 50f;       
    public float fireDamage = 2.0f;     
    public float hatredCostPerShot = 10f; 

    private void Awake()
    {
        controls = new PlayerControls();
        cameraTransform = GetComponentInChildren<Camera>().transform;
    }

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Attack.performed += ctx => ShootWeapon();
    }

    private void OnDisable()
    {
        controls.Disable();
        controls.Player.Attack.performed -= ctx => ShootWeapon();
    }

    private void ShootWeapon()
    {
        if (!AdamState.Instance.SpendHatred(hatredCostPerShot))
        {
            Debug.LogWarning("❌ GUN CLICK! Out of Hatred!");
            return;
        }

        // Use the new method so the UI updates
        AdamState.Instance.ConsumeAmmo();
        Debug.Log($"💥 Shot fired! Ammo Remaining: {AdamState.Instance.currentAmmo}");

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hitData;

        Debug.DrawRay(cameraTransform.position, cameraTransform.forward * fireRange, Color.cyan, 0.2f);

        if (Physics.Raycast(ray, out hitData, fireRange))
        {
            // Universal Damage System
            IDamageable target = hitData.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(fireDamage, cameraTransform.forward);
            }
        }

        if (AdamState.Instance.currentAmmo <= 0)
        {
            // Use the new method so the UI updates
            AdamState.Instance.ReloadAmmo();
            Debug.Log("🔄 Firearm mechanics cycled.");
        }
    }
}