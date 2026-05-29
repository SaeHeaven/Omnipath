using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class AdamController : MonoBehaviour
{
    private PlayerControls controls;
    private CharacterController characterController;
    private Transform cameraTransform;

    [Header("Base Movement")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 10f;
    private float currentSpeed;

    [Header("Parkour: Dash")]
    public float dashDistance = 8f;   // Tweak this to dictate exactly how far you travel
    public float dashDuration = 0.2f; // Tweak this for how fast the dash happens
    public float dashCooldown = 1f;
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

    [Header("Parkour: Propulsion Math")]
    public float propulsionMultiplier = 1.5f; // Tweak this to make launches more violent!
    public float minPropulsionAngle = 15f;    // Prevents purely flat ground-skims
    private Vector3 airMomentum = Vector3.zero;

    [Header("Physics & Look")]
    public float gravity = -25f;
    public float jumpHeight = 2.5f;
    public float mouseSensitivity = 15f;
    
    private float cameraVerticalRotation = 0f;
    private Vector3 verticalVelocity;
    private Vector2 moveInput;
    private Vector2 lookInput;

    private void Awake()
    {
        controls = new PlayerControls();
        characterController = GetComponent<CharacterController>();
        cameraTransform = GetComponentInChildren<Camera>().transform;

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
        HandleParkourMovement();
    }

    private void HandleParkourMovement()
    {
        bool isGrounded = characterController.isGrounded;

        // 1. Friction & Air Drag
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
            if (!isSliding) 
                airMomentum = Vector3.Lerp(airMomentum, Vector3.zero, Time.deltaTime * 5f);
        }
        else
        {
            airMomentum = Vector3.Lerp(airMomentum, Vector3.zero, Time.deltaTime * 1.5f);
        }

        // 2. Read Inputs
        bool isSprinting = controls.Player.Sprint.ReadValue<float>() > 0;
        bool isCrouchHeld = controls.Player.Crouch.ReadValue<float>() > 0;
        Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;

        // 3. TRIGGER SLIDE
        if (controls.Player.Crouch.triggered && isSprinting && isGrounded && !isSliding)
        {
            isSliding = true;
            slideDirection = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;
            currentSlideSpeed = slideEntrySpeed; 
        }

        // 4. Heights & Cameras
        float targetHeight = standingHeight;
        float targetCamY = 0.6f;

        if (isSliding) 
        {
            targetHeight = slideHeight;
            targetCamY = -0.4f;
        }
        else if (isCrouchHeld) 
        {
            targetHeight = crouchHeight;
            targetCamY = 0.1f;
        }

        characterController.height = Mathf.Lerp(characterController.height, targetHeight, 10f * Time.deltaTime);
        Vector3 camPos = cameraTransform.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, targetCamY, 10f * Time.deltaTime);
        cameraTransform.localPosition = camPos;

        // 5. Final Horizontal Velocity calculations
        Vector3 finalHorizontalVelocity = Vector3.zero;

        if (dashCooldownTimer > 0) dashCooldownTimer -= Time.deltaTime;

        if (controls.Player.Dash.triggered && dashCooldownTimer <= 0 && !isDashing && !isSliding)
        {
            isDashing = true;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            dashDirection = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            // Calculate exact speed needed to cover the desired Dash Distance
            float calculatedDashSpeed = dashDistance / dashDuration;
            finalHorizontalVelocity = dashDirection * calculatedDashSpeed;
            if (dashTimer <= 0) isDashing = false;
        }
        else if (isSliding)
        {
            float activeFriction = (moveInput.y < -0.1f) ? slideBrakeFriction : slideNormalFriction;
            currentSlideSpeed -= activeFriction * Time.deltaTime;

            if (currentSlideSpeed <= 1f)
            {
                isSliding = false; 
            }
            else
            {
                finalHorizontalVelocity = slideDirection * currentSlideSpeed;
            }
        }
        else
        {
            currentSpeed = isCrouchHeld ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
            finalHorizontalVelocity = (moveDirection * currentSpeed) + airMomentum;
        }

        // 6. Jump & Propulsion Engine
        if (controls.Player.Jump.triggered && isGrounded && !isDashing)
        {
            if (isSliding)
            {
                float pitch = -cameraVerticalRotation;
                
                // Enforce the strict 15-degree minimum launch angle
                float mathPitch = Mathf.Clamp(pitch, minPropulsionAngle, 90f);
                
                float verticalMult = Mathf.Clamp01(mathPitch / 45f);
                float horizontalMult = Mathf.Clamp01((90f - mathPitch) / 45f);

                // Multiply your CURRENT sliding speed by the tweakable inspector variable
                float rawLaunchPower = currentSlideSpeed * propulsionMultiplier;

                // Add explosive force to the base jump
                float totalVerticalLift = jumpHeight + (rawLaunchPower * 0.4f * verticalMult);
                verticalVelocity.y = Mathf.Sqrt(totalVerticalLift * -2f * gravity);

                Vector3 lookDir = cameraTransform.forward;
                lookDir.y = 0;
                airMomentum += lookDir.normalized * (rawLaunchPower * horizontalMult);
                
                isSliding = false; // Break out of slide instantly
                
                Debug.Log($"🚀 PROPULSION! Pitch: {mathPitch:F0}° | Speed at Launch: {currentSlideSpeed:F1} | Raw Power: {rawLaunchPower:F1}");
            }
            else
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        // 7. Apply gravity and execute
        verticalVelocity.y += gravity * Time.deltaTime;
        finalHorizontalVelocity.y = verticalVelocity.y;

        characterController.Move(finalHorizontalVelocity * Time.deltaTime);
    }

    private void HandleLook()
    {
        transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity * Time.deltaTime);
        cameraVerticalRotation -= lookInput.y * mouseSensitivity * Time.deltaTime;
        cameraVerticalRotation = Mathf.Clamp(cameraVerticalRotation, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(cameraVerticalRotation, 0f, 0f);
    }
}