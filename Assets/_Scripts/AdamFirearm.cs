using UnityEngine;

public class AdamFirearm : MonoBehaviour
{
    private PlayerControls controls;
    private Transform cameraTransform;

    [Header("Ballistic Settings")]
    public float fireRange = 50f;       // Long range firearm limits
    public float fireDamage = 2.0f;     // Deals double punch damage
    public float hatredCostPerShot = 10f; // Costs 10 Hatred points to fire

    private void Awake()
    {
        controls = new PlayerControls();
        cameraTransform = GetComponentInChildren<Camera>().transform;
    }

    private void OnEnable()
    {
        controls.Enable();
        // Listen for our universal attack input trigger
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

        AdamState.Instance.currentAmmo--;
        Debug.Log($"💥 Shot fired! Ammo Remaining: {AdamState.Instance.currentAmmo}");

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hitData;

        Debug.DrawRay(cameraTransform.position, cameraTransform.forward * fireRange, Color.cyan, 0.2f);

        if (Physics.Raycast(ray, out hitData, fireRange))
        {
            // UPGRADED: Scan specifically for our new moving AI script
            EnemyBrain target = hitData.collider.GetComponent<EnemyBrain>();
            if (target != null)
            {
                // Ballistic firearm transfers high momentum forces across distances
                Vector3 knockbackDir = cameraTransform.forward;
                target.TakeDamage(fireDamage, knockbackDir);
            }
            YuriBoss boss = hitData.collider.GetComponent<YuriBoss>();
            if (boss != null)
            {
                boss.TakeBossDamage(fireDamage);
            }
        }

        if (AdamState.Instance.currentAmmo <= 0)
        {
            AdamState.Instance.currentAmmo = AdamState.Instance.maxAmmo;
            Debug.Log("🔄 Firearm mechanics cycled.");
        }
    }
}