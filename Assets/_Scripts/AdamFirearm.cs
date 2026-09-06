using UnityEngine;

public class AdamFirearm : MonoBehaviour
{
    private PlayerControls controls;
    private Transform cameraTransform;

    [Header("Ballistic Settings")]
    public float fireRange = 50f;            
    public float fireDamage = 2.0f;          
    public float staminaCostPerShot = 3.0f; // Micro-tax per shot

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
        // 1. Enforce Micro-Tax Check
        if (AdamState.Instance == null || !AdamState.Instance.ConsumeStamina(staminaCostPerShot))
        {
            Debug.LogWarning("🛑 GUN CLICK! Out of Stamina or Exhausted!");
            return;
        }

        // 2. Consume Ammo
        AdamState.Instance.ConsumeAmmo();
        Debug.Log($"🔫 Shot fired! Ammo Remaining: {AdamState.Instance.currentAmmo} | Stamina Tax: -{staminaCostPerShot}");

        // 3. Ballistic Raycast
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hitData;
        Debug.DrawRay(cameraTransform.position, cameraTransform.forward * fireRange, Color.cyan, 0.2f);

        if (Physics.Raycast(ray, out hitData, fireRange))
        {
            IDamageable target = hitData.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(fireDamage, cameraTransform.forward);
            }
        }

        if (AdamState.Instance.currentAmmo <= 0)
        {
            AdamState.Instance.ReloadAmmo();
            Debug.Log("🔄 Firearm mechanics cycled.");
        }
    }
}