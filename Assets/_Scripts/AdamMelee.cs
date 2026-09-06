using UnityEngine;
using UnityEngine.InputSystem;

public class AdamMelee : MonoBehaviour
{
    public static AdamMelee Instance { get; private set; } // Singleton reference for hit callbacks

    private PlayerControls controls;
    private Transform cameraTransform;
    private AdamController playerController;

    [Header("Melee Settings")]
    public float punchRange = 2.5f; 
    public float punchDamage = 1.0f; 

    [Header("Option 3B: Risk/Reward Stamina Tuning")]
    public float maxPunchUpfrontCost = 20f; 
    public float minPunchUpfrontCost = 5f;  
    public float hitRefundBonus = 15f;      

    [Header("FPS Viewmodel Animation")]
    public Animator armsAnimator;
    [Tooltip("Exact name of your Block state node inside the Animator Controller.")]
    public string blockStateName = "Block"; 

    [Header("--- CAMERA PUNCH ANIMATION ---")]
    public Transform cameraPunchPivot;
    public float punchRecoilPitch = 3.0f;
    public float punchRecoilYaw = 2.5f;
    public float punchRecoilRoll = 1.5f;
    public float punchForwardThrust = 0.15f;
    public float recoilSnappiness = 20f;
    public float recoilReturnSpeed = 10f;

    private Vector3 currentRotationOffset;
    private Vector3 targetRotationOffset;
    private Vector3 currentPositionOffset;
    private Vector3 targetPositionOffset;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        controls = new PlayerControls();
        cameraTransform = GetComponentInChildren<Camera>().transform;
        playerController = GetComponentInParent<AdamController>();

        if (armsAnimator == null)
        {
            armsAnimator = GetComponentInChildren<Animator>();
        }
    }

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Attack.performed += ctx => ExecutePunch();
    }

    private void OnDisable()
    {
        controls.Disable();
        controls.Player.Attack.performed -= ctx => ExecutePunch();

        if (AdamState.Instance != null)
        {
            AdamState.Instance.isBlocking = false;
        }
        if (armsAnimator != null)
        {
            armsAnimator.SetBool("IsBlocking", false);
        }
    }

    private void Update()
    {
        targetRotationOffset = Vector3.Lerp(targetRotationOffset, Vector3.zero, Time.deltaTime * recoilReturnSpeed);
        currentRotationOffset = Vector3.Slerp(currentRotationOffset, targetRotationOffset, Time.deltaTime * recoilSnappiness);

        targetPositionOffset = Vector3.Lerp(targetPositionOffset, Vector3.zero, Time.deltaTime * recoilReturnSpeed);
        currentPositionOffset = Vector3.Lerp(currentPositionOffset, targetPositionOffset, Time.deltaTime * recoilSnappiness);

        if (cameraPunchPivot != null)
        {
            cameraPunchPivot.localRotation = Quaternion.Euler(currentRotationOffset);
            cameraPunchPivot.localPosition = currentPositionOffset;
        }

        // Read right-click block hold state
        bool rightClickHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;
        bool canBlock = (AdamState.Instance != null && !AdamState.Instance.isExhausted);
        bool isCurrentlyBlocking = rightClickHeld && canBlock;

        if (AdamState.Instance != null)
        {
            AdamState.Instance.isBlocking = isCurrentlyBlocking;
        }

        if (armsAnimator != null)
        {
            armsAnimator.SetBool("IsBlocking", isCurrentlyBlocking);
        }
    }

    /// <summary>
    /// Restarts the block animation from frame 0 when hit while guarding.
    /// </summary>
    public void OnBlockHit()
    {
        if (armsAnimator != null)
        {
            // Forces the Animator state back to normalized time 0.0f
            armsAnimator.Play(blockStateName, 0, 0f);
        }
    }

    private void ExecutePunch()
    {
        if (AdamState.Instance != null && AdamState.Instance.isBlocking) return;

        float momentumRatio = playerController != null ? playerController.GetMomentumRatio() : 0f;
        float actualUpfrontCost = Mathf.Lerp(maxPunchUpfrontCost, minPunchUpfrontCost, momentumRatio);

        if (AdamState.Instance == null || !AdamState.Instance.ConsumeStamina(actualUpfrontCost))
        {
            Debug.LogWarning("  PUNCH ABORTED! Not enough stamina or Exhausted!");
            return;
        }

        bool isLeftPunch = Random.value > 0.5f;

        if (armsAnimator != null)
        {
            if (isLeftPunch)
            {
                armsAnimator.ResetTrigger("PunchRight");
                armsAnimator.SetTrigger("PunchLeft");
            }
            else
            {
                armsAnimator.ResetTrigger("PunchLeft");
                armsAnimator.SetTrigger("PunchRight");
            }
        }

        TriggerCameraPunch(isLeftPunch);

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hitData;

        if (Physics.Raycast(ray, out hitData, punchRange))
        {
            IDamageable target = hitData.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(punchDamage, cameraTransform.forward);
                float totalRefundAmount = actualUpfrontCost + hitRefundBonus;
                AdamState.Instance.RefundStamina(totalRefundAmount);
            }
        }
    }

    private void TriggerCameraPunch(bool isLeftPunch)
    {
        float sideSign = isLeftPunch ? 1f : -1f;

        targetRotationOffset += new Vector3(
            -punchRecoilPitch, 
            punchRecoilYaw * sideSign, 
            -punchRecoilRoll * sideSign
        );

        targetPositionOffset += new Vector3(0f, 0f, punchForwardThrust);
    }
}