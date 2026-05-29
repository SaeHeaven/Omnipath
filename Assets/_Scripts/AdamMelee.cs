using UnityEngine;

public class AdamMelee : MonoBehaviour
{
    private PlayerControls controls;
    private Transform cameraTransform;

    [Header("Melee Settings")]
    public float punchRange = 2.5f; // How close you must stand to hit
    public float punchDamage = 1.0f; // Damage dealt per punch

    private void Awake()
    {
        controls = new PlayerControls();
        
        // Find the camera nestled inside Adam's hierarchy
        cameraTransform = GetComponentInChildren<Camera>().transform;
    }

    private void OnEnable()
    {
        controls.Enable();
        // Listen specifically for the Left Click performance trigger
        controls.Player.Attack.performed += ctx => ExecutePunch();
    }

    private void OnDisable()
    {
        controls.Disable();
        controls.Player.Attack.performed -= ctx => ExecutePunch();
    }
private void ExecutePunch()
    {
        Debug.Log("Adam threw a punch!");

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hitData;

        Debug.DrawRay(cameraTransform.position, cameraTransform.forward * punchRange, Color.red, 0.4f);

        if (Physics.Raycast(ray, out hitData, punchRange))
        {
            // UPGRADED: Scan specifically for our new moving AI script
            EnemyBrain target = hitData.collider.GetComponent<EnemyBrain>();

            if (target != null)
            {
                // Calculate directional knockback using camera direction
                Vector3 knockbackDir = cameraTransform.forward;
                
                target.TakeDamage(punchDamage, knockbackDir);
                AdamState.Instance.GainHatred(15f); 
            }
            YuriBoss boss = hitData.collider.GetComponent<YuriBoss>();
            if (boss != null)
            {
                boss.TakeBossDamage(punchDamage);
                AdamState.Instance.GainHatred(15f);
            }
        }
    }
}