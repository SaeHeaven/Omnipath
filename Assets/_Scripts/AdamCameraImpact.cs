using UnityEngine;

public class AdamCameraImpact : MonoBehaviour
{
    public static AdamCameraImpact Instance { get; private set; }

    [Header("Damage Scaling Settings")]
    [Tooltip("Percentage of Adam's Max Health that triggers 100% max camera impact (e.g., 0.75 = 75% of max health).")]
    [Range(0.05f, 1f)] public float maxDamageHealthPercent = 0.75f;

    [Header("Max Rotational Hit Recoil Limits (At 100% Intensity)")]
    public float maxPitchTilt = 8f;   
    public float maxYawTilt = 6f;     
    public float maxRollTilt = 5f;    

    [Header("Max Positional Hit Recoil (At 100% Intensity)")]
    public float maxPositionPush = 0.2f; 

    [Header("Physics Recovery Settings")]
    public float snappiness = 25f;    
    public float returnSpeed = 12f;   

    private Vector3 targetRotation;
    private Vector3 currentRotation;
    private Vector3 targetPosition;
    private Vector3 currentPosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        targetRotation = Vector3.Lerp(targetRotation, Vector3.zero, Time.deltaTime * returnSpeed);
        currentRotation = Vector3.Slerp(currentRotation, targetRotation, Time.deltaTime * snappiness);

        targetPosition = Vector3.Lerp(targetPosition, Vector3.zero, Time.deltaTime * returnSpeed);
        currentPosition = Vector3.Lerp(currentPosition, targetPosition, Time.deltaTime * snappiness);

        transform.localRotation = Quaternion.Euler(currentRotation);
        transform.localPosition = currentPosition;
    }

    /// <summary>
    /// Triggers directional camera recoil and hit FOV expansion scaled dynamically by damage.
    /// </summary>
    public void TriggerHitImpact(Vector3 hitDirection, float damageAmount, float maxHealth)
    {
        if (hitDirection == Vector3.zero || maxHealth <= 0f) return;

        float maxDamageThreshold = maxHealth * maxDamageHealthPercent;
        float damageScale = Mathf.Clamp01(damageAmount / maxDamageThreshold);

        // 1. Trigger camera hit FOV expansion in AdamController
        if (AdamController.Instance != null)
        {
            AdamController.Instance.TriggerHitFOV(damageScale);
        }

        // 2. Apply directional rotational and positional camera tilt
        Vector3 localHitDir = transform.root.InverseTransformDirection(hitDirection.normalized);

        float pitch = -localHitDir.z * maxPitchTilt * damageScale;
        float yaw = -localHitDir.x * maxYawTilt * damageScale;
        float roll = localHitDir.x * maxRollTilt * damageScale;

        targetRotation += new Vector3(pitch, yaw, roll);
        targetPosition += -localHitDir * maxPositionPush * damageScale;
    }
}