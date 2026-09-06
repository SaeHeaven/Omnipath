using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class AdamController : MonoBehaviour
{
    public static AdamController Instance { get; private set; }

    private PlayerControls controls;
    private CharacterController characterController;
    private Transform cameraTransform;
    private Camera playerCamera;

    [Header("Base Movement & Dynamic Sprint Stamina")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 10f;
    public float maxSprintStaminaCostPerSec = 25f; 
    public float minSprintStaminaCostPerSec = 8f;  
    private float currentSpeed;

    [Header("Parkour: Dash & Gear Efficiency")]
    public float dashDistance = 8f;        
    public float dashDuration = 0.2f;      
    public float dashCooldown = 1f;        
    public float maxDashStaminaCost = 40f; 
    public float minDashStaminaCost = 5f;  
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;
    private bool isDashing = false;
    private Vector3 dashDirection;

    [Header("Parkour: Height States")]
    public float standingHeight = 2f;
    public float crouchHeight = 1.2f;
    public float slideHeight = 0.5f;
    public float crouchSpeed = 4f;

    [Header("Parkour: Momentum Slide")]
    public float slideEntrySpeed = 18f;
    public float slideNormalFriction = 12f;
    public float slideBrakeFriction = 35f;
    private bool isSliding = false;
    private float currentSlideSpeed = 0f;
    private Vector3 slideDirection;

    [Header("Parkour: Propulsion Math & Stamina")]
    public float propulsionMultiplier = 1.5f; 
    public float minPropulsionAngle = 15f;    
    public float maxPropulsionStaminaCost = 30f; 
    public float minPropulsionStaminaCost = 10f; 
    private Vector3 airMomentum = Vector3.zero;

    [Header("Physics & Look")]
    public float gravity = -25f;
    public float jumpHeight = 2.5f;
    public float mouseSensitivity = 15f; 
    private float cameraVerticalRotation = 0f;
    private Vector3 verticalVelocity;
    private Vector2 moveInput;
    private Vector2 lookInput;

    [Header("--- CAMERA POLISH & DYNAMIC FOV ---")]
    public float baseFOV = 80f;
    public float maxSpeedFOVBonus = 12f;
    [Tooltip("Total 3D physical speed (m/s) required to hit 100% max FOV expansion.")]
    public float maxVelocityForFOV = 25f;
    public float blockFOVReduction = 6f;
    public float maxHitFOVExpansion = 8f;
    public float maxStrafeTilt = 2.5f;
    public float tiltSmoothSpeed = 10f;
    public float fovSmoothSpeed = 8f;

    private float currentStrafeTilt = 0f;
    private float currentHitFovImpulse = 0f;

    [Header("FPS Viewmodel Animation")]
    public Animator armsAnimator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        controls = new PlayerControls();
        characterController = GetComponent<CharacterController>();
        cameraTransform = GetComponentInChildren<Camera>().transform;
        
        if (cameraTransform != null)
        {
            playerCamera = cameraTransform.GetComponent<Camera>();
        }

        if (armsAnimator == null)
        {
            armsAnimator = GetComponentInChildren<Animator>();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void Update()
    {
        moveInput = controls.Player.Move.ReadValue<Vector2>();
        lookInput = controls.Player.Look.ReadValue<Vector2>();

        HandleLook();
        HandleCameraDynamicFOV();
        HandleParkourMovement();
        UpdateArmsLocomotion();
    }

    public float GetMomentumRatio()
    {
        Vector3 horizontalVel = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
        return Mathf.Clamp01(horizontalVel.magnitude / sprintSpeed);
    }

    private void HandleLook()
    {
        transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity * Time.deltaTime);
        cameraVerticalRotation -= lookInput.y * mouseSensitivity * Time.deltaTime;
        cameraVerticalRotation = Mathf.Clamp(cameraVerticalRotation, -90f, 90f);

        float targetTilt = -moveInput.x * maxStrafeTilt;
        currentStrafeTilt = Mathf.Lerp(currentStrafeTilt, targetTilt, Time.deltaTime * tiltSmoothSpeed);

        cameraTransform.localRotation = Quaternion.Euler(cameraVerticalRotation, 0f, currentStrafeTilt);
    }

    private void HandleCameraDynamicFOV()
    {
        if (playerCamera == null) return;

        float current3DSpeed = characterController.velocity.magnitude;
        float speedRatio = Mathf.Clamp01(current3DSpeed / maxVelocityForFOV);

        bool isBlocking = (AdamState.Instance != null && AdamState.Instance.isBlocking);

        currentHitFovImpulse = Mathf.Lerp(currentHitFovImpulse, 0f, Time.deltaTime * fovSmoothSpeed);

        float speedFOV = speedRatio * maxSpeedFOVBonus;
        float blockFOV = isBlocking ? blockFOVReduction : 0f;

        float targetFOV = baseFOV + speedFOV + currentHitFovImpulse - blockFOV;

        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * fovSmoothSpeed);
    }

    public void TriggerHitFOV(float damageScale)
    {
        currentHitFovImpulse = maxHitFOVExpansion * damageScale;
    }

    /// <summary>
    /// Updates viewmodel ground locomotion, air parameters, and dynamic running playback speed.
    /// </summary>
    private void UpdateArmsLocomotion()
    {
        if (armsAnimator == null) return;

        bool isGrounded = IsGroundedBuffered();
        
        // Calculate 2D horizontal movement speed
        Vector3 horizontalVel = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
        float currentHorizontalSpeed = horizontalVel.magnitude;

        // Player is considered running if grounded and moving faster than base walk speed
        bool isRunning = isGrounded && currentHorizontalSpeed > (walkSpeed + 0.5f);

        // Calculate dynamic playback multiplier (1.0 at base sprintSpeed, scales up/down with velocity)
        float animSpeedMultiplier = Mathf.Max(0.1f, currentHorizontalSpeed / sprintSpeed);

        // Update Animator parameters
        armsAnimator.SetBool("IsGrounded", isGrounded);
        armsAnimator.SetFloat("AirSpeed", characterController.velocity.magnitude);
        armsAnimator.SetBool("IsRunning", isRunning);
        armsAnimator.SetFloat("RunAnimSpeed", animSpeedMultiplier);
    }

    private void HandleParkourMovement()
    {
        bool isGrounded = IsGroundedBuffered();

        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -12f; 
            if (!isSliding) 
                airMomentum = Vector3.Lerp(airMomentum, Vector3.zero, Time.deltaTime * 5f);
        }
        else
        {
            airMomentum = Vector3.Lerp(airMomentum, Vector3.zero, Time.deltaTime * 1.5f);
        }

        bool isBlocking = (AdamState.Instance != null && AdamState.Instance.isBlocking);
        
        // Mid-Air Crouch Check: Crouch input is ONLY valid when grounded
        bool isCrouchHeld = controls.Player.Crouch.ReadValue<float>() > 0 && isGrounded;
        bool isSprintingInput = controls.Player.Sprint.ReadValue<float>() > 0;
        Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;
        bool isFacingUphill = CheckIfMovingUphill(moveDirection);

        if (controls.Player.Crouch.triggered && isSprintingInput && isGrounded && !isSliding && !isFacingUphill && !isBlocking && (AdamState.Instance != null && !AdamState.Instance.isExhausted))
        {
            isSliding = true;
            slideDirection = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;
            currentSlideSpeed = slideEntrySpeed;
        }

        bool isActuallySprinting = isGrounded && !isSliding && !isDashing && !isBlocking && isSprintingInput && moveInput.magnitude > 0.1f && !isCrouchHeld && (AdamState.Instance != null && !AdamState.Instance.isExhausted);
        if (isActuallySprinting)
        {
            float momentumRatio = GetMomentumRatio();
            float dynamicSprintCostPerSec = Mathf.Lerp(maxSprintStaminaCostPerSec, minSprintStaminaCostPerSec, momentumRatio);
            if (!AdamState.Instance.ConsumeStamina(dynamicSprintCostPerSec * Time.deltaTime))
            {
                isActuallySprinting = false; 
            }
        }

        // HEIGHT & CAPSULE CENTER MANAGEMENT
        float targetHeight = standingHeight;
        float targetCamY = 0.6f;

        if (isSliding) 
        {
            targetHeight = slideHeight;
            targetCamY = -0.3f;
        }
        else if (isCrouchHeld) // Will only trigger if grounded
        {
            targetHeight = crouchHeight;
            targetCamY = 0.1f;
        }

        characterController.height = Mathf.Lerp(characterController.height, targetHeight, 10f * Time.deltaTime);

        // Anchor capsule bottom to feet
        Vector3 currentCenter = characterController.center;
        currentCenter.y = (characterController.height - standingHeight) / 2f;
        characterController.center = currentCenter;

        Vector3 camPos = cameraTransform.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, targetCamY, 10f * Time.deltaTime);
        cameraTransform.localPosition = camPos;

        Vector3 finalHorizontalVelocity = Vector3.zero;
        if (dashCooldownTimer > 0) dashCooldownTimer -= Time.deltaTime;

        // Dash disabled while blocking
        if (controls.Player.Dash.triggered && dashCooldownTimer <= 0 && !isDashing && !isSliding && !isBlocking)
        {
            float momentumRatio = GetMomentumRatio();
            float dynamicDashCost = Mathf.Lerp(maxDashStaminaCost, minDashStaminaCost, momentumRatio);
            if (AdamState.Instance != null && AdamState.Instance.ConsumeStamina(dynamicDashCost))
            {
                isDashing = true;
                dashTimer = dashDuration;
                dashCooldownTimer = dashCooldown;
                dashDirection = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;
            }
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            float calculatedDashSpeed = dashDistance / dashDuration;
            finalHorizontalVelocity = dashDirection * calculatedDashSpeed;
            if (dashTimer <= 0) isDashing = false;
        }
        else if (isSliding)
        {
            float activeFriction = (moveInput.y < -0.1f) ? slideBrakeFriction : slideNormalFriction;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hitInfo, characterController.height + 0.8f))
            {
                Vector3 slopeSlideDir = Vector3.ProjectOnPlane(slideDirection, hitInfo.normal).normalized;
                float slopeGrade = slopeSlideDir.y;
                if (slopeGrade > 0.05f) 
                {
                    isSliding = false;
                }
                else if (slopeGrade < -0.05f) 
                {
                    float downhillAccel = 35f * Mathf.Abs(slopeGrade);
                    currentSlideSpeed += downhillAccel * Time.deltaTime;
                    finalHorizontalVelocity = slopeSlideDir * currentSlideSpeed;
                }
                else
                {
                    finalHorizontalVelocity = slideDirection * currentSlideSpeed;
                }
            }
            else
            {
                finalHorizontalVelocity = slideDirection * currentSlideSpeed;
            }
            currentSlideSpeed -= activeFriction * Time.deltaTime;
            if (currentSlideSpeed <= 1f)
            {
                isSliding = false;
            }
        }
        else
        {
            if (isGrounded)
            {
                if (isBlocking)
                {
                    currentSpeed = walkSpeed * 0.75f;
                }
                else
                {
                    currentSpeed = isCrouchHeld ? crouchSpeed : (isActuallySprinting ? sprintSpeed : walkSpeed);
                }
            }
            else
            {
                currentSpeed = walkSpeed;
            }
            finalHorizontalVelocity = (moveDirection * currentSpeed) + airMomentum;
        }

        // Jump & Propulsion disabled while blocking
        if (controls.Player.Jump.triggered && isGrounded && !isDashing && !isBlocking)
        {
            if (armsAnimator != null)
            {
                armsAnimator.SetTrigger("Jump");
            }

            if (isSliding)
            {
                float slideRatio = Mathf.Clamp01(currentSlideSpeed / slideEntrySpeed);
                float dynamicPropulsionCost = Mathf.Lerp(maxPropulsionStaminaCost, minPropulsionStaminaCost, slideRatio);
                if (AdamState.Instance != null && AdamState.Instance.ConsumeStamina(dynamicPropulsionCost))
                {
                    float pitch = -cameraVerticalRotation;
                    float mathPitch = Mathf.Clamp(pitch, minPropulsionAngle, 90f);
                    
                    float verticalMult = Mathf.Clamp01(mathPitch / 45f);
                    float horizontalMult = Mathf.Clamp01((90f - mathPitch) / 45f);
                    float rawLaunchPower = currentSlideSpeed * propulsionMultiplier;
                    float totalVerticalLift = jumpHeight + (rawLaunchPower * 0.4f * verticalMult);
                    verticalVelocity.y = Mathf.Sqrt(totalVerticalLift * -2f * gravity);
                    Vector3 lookDir = cameraTransform.forward;
                    lookDir.y = 0;
                    airMomentum += lookDir.normalized * (rawLaunchPower * horizontalMult);
                }
                else
                {
                    verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
                isSliding = false; 
            }
            else
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -12f; 
        }
        verticalVelocity.y += gravity * Time.deltaTime;
        finalHorizontalVelocity.y = verticalVelocity.y;
        characterController.Move(finalHorizontalVelocity * Time.deltaTime);

        if ((characterController.collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity.y > 0)
        {
            verticalVelocity.y = -2f;
        }
    }

    private bool IsGroundedBuffered()
    {
        if (characterController.isGrounded) return true;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;
        return Physics.Raycast(rayOrigin, Vector3.down, 1.2f);
    }

    private bool CheckIfMovingUphill(Vector3 moveDir)
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1.2f))
        {
            Vector3 checkDir = moveDir.magnitude > 0.1f ? moveDir.normalized : transform.forward;
            Vector3 slopeDir = Vector3.ProjectOnPlane(checkDir, hit.normal).normalized;
            return slopeDir.y > 0.08f; 
        }
        return false;
    }
}