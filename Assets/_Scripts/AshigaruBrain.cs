using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AshigaruBrain : MonoBehaviour, IDamageable
{
    public enum AIState { Unaware, Suspicious, Chasing, CombatEngaged }

    [Header("--- CURRENT ACTIVE STATE ---")]
    [SerializeField] private AIState currentState = AIState.Unaware;
    [Range(0f, 100f)] public float awareness = 0f;
    public bool hasBeenAlerted = false;

    [Header("--- HEALTH & HIT REACTIONS ---")]
    public float maxHealth = 100f;
    public float currentHealth;
    public bool isDead { get; private set; } = false;

    [Header("--- COMBAT & THRUST ATTACK ---")]
    public float attackCooldown = 2.5f;
    public float attackDamage = 20f;
    public float thrustRange = 3.0f;
    public float attackRangeBuffer = 1.0f;
    public float attackWindupTime = 0.4f;
    public float attackLockDuration = 1.2f;
    private float attackTimer = 0f;
    private bool isAttacking = false;

    [Header("--- HEAVY SMASH ATTACK SYSTEM ---")]
    [Range(0f, 1f)] public float heavyChanceDefensive = 0.65f;
    [Range(0f, 1f)] public float heavyChanceAggressive = 0.25f;
    public float heavySmashDamage = 40f;
    public float heavySmashRange = 3.2f;
    public float heavyWindupDuration = 0.8f;
    public float heavySmashWindupTime = 0.5f;
    public float heavySmashLockDuration = 1.6f;
    public float heavyHoldMaxDuration = 5.0f;

    private bool isHeavyHolding = false;
    private bool isHeavySmashing = false;
    private float heavyHoldTimer = 0f;

    [Header("--- TWO-STEP CHARGE ATTACK ---")]
    public float chargeAttackDamage = 35f;
    public float chargeMinDistance = 4.5f;
    [Range(0f, 1f)] public float chargeChanceAggressive = 0.75f;
    [Range(0f, 1f)] public float chargeChanceDefensive = 0.15f;

    public float dash1Speed = 3.0f;
    public float dash1Duration = 0.2f;
    public float dashDelay = 0.35f;
    public float dash2Speed = 10.0f;
    public float dash2Duration = 0.5f;
    public float chargeRecoveryDuration = 0.4f;

    private bool isCharging = false;
    private bool chargeHitDealt = false;

    [Header("--- ADVANCED COMBAT POSTURE ---")]
    public float combatAdvanceSpeed = 2.0f;
    public float combatBackstepSpeed = 1.5f;
    public float combatOrbitSpeed = 1.5f;
    
    private enum CombatPosture { Aggressive, Defensive }
    [SerializeField] private CombatPosture currentPosture = CombatPosture.Aggressive;
    
    private float postureLockTimer = 0f;
    private float retreatTimer = 0f;
    private float orbitTimer = 0f;
    private int orbitDirection = 1;
    private Vector3 prevPlayerPos;

    [Header("--- PATROL & SEARCH SYSTEM ---")]
    public PatrolRoute assignedPatrolRoute;
    public float searchRadius = 8.0f;
    public float huntDuration = 15.0f;
    public float franticJogSpeed = 4.5f;

    [Header("--- AWARENESS CONFIGURATION ---")]
    public float baseBuildRate = 8.0f;
    public float decayRate = 3.0f;
    public float visionLeniency = 0.2f;

    [Header("--- MOVEMENT DYNAMICS ---")]
    [Range(10f, 90f)] public float investigateThreshold = 40.0f;
    public float suspiciousWalkSpeed = 2.5f;
    public float chasingRunSpeed = 7.0f;
    public float suspiciousAcceleration = 8.0f;
    public float chasingAcceleration = 60.0f;
    public float suspiciousTurnSpeed = 120.0f;
    public float chasingTurnSpeed = 800.0f;

    [Header("--- DETECTION ZONES ---")]
    public float chaseMemoryDuration = 7.0f;
    public float lowAwarenessMemoryDuration = 3.0f;
    public float escapeRadius = 40.0f;
    public float detectionRadius = 15.0f;
    [Range(10f, 180f)] public float viewAngle = 90.0f;
    public float visionThickness = 0.35f;
    public float combatEngageRadius = 5.0f;
    public float eyeHeightOffset = 1.5f;

    [Header("--- DEBUG VISUALIZATION ---")]
    public bool showRuntime3DMeshes = true;
    public bool showEditorGizmos = true;
    public Vector3 hitBoxDimensions = new Vector3(1.2f, 1.8f, 1.2f);
    public Transform hitBoxOffset;

    private Transform playerTarget;
    private NavMeshAgent agent;
    private Animator animator;
    private float pathTimer;
    private float pathUpdateTime = 0.1f;

    private Vector3 lastKnownPosition;
    private bool hasLastKnownPosition = false;
    private bool isPlayerVisible = false;
    private float currentChaseTimer = 0f;
    private float currentGraceTimer = 0f;
    private float currentLowAwarenessTimer = 0f;
    private bool isDynamicSearching = false;
    private float huntTimer = 0f;
    private float searchWaitTimer = 0f;
    private Vector3 currentSearchDestination;
    private int currentPatrolIndex = 0;
    private float patrolWaitTimer = 0f;
    private bool isWaitingAtPatrolNode = false;

    private GameObject debugContainer;
    private GameObject coneVisual;
    private GameObject combatVisual;
    private GameObject escapeVisual;
    private GameObject hitboxVisual;
    private MeshFilter coneMeshFilter;
    private Material coneMat;
    private Material combatMat;
    private Material escapeMat;
    private bool lastVisualState;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        lastVisualState = showRuntime3DMeshes;
        agent.autoBraking = true;
        currentHealth = maxHealth;
        
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
            prevPlayerPos = playerTarget.position;
        }
        
        if (showRuntime3DMeshes) CreateDebugGeometry();
    }

    private void Update()
    {
        if (isDead) return;

        if (showRuntime3DMeshes != lastVisualState)
        {
            ToggleDebugGeometry(showRuntime3DMeshes);
            lastVisualState = showRuntime3DMeshes;
        }
        if (showRuntime3DMeshes && debugContainer != null) UpdateGeometryTransforms();

        UpdateAnimator();
        if (playerTarget == null) return;

        EvaluateVisionAndAwareness();
        ExecuteStateBehaviors();

        prevPlayerPos = playerTarget.position;
    }

    /// <summary>
    /// IDamageable Interface Implementation
    /// </summary>
    public void TakeDamage(float amount, Vector3 knockbackDir)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"<color=orange>[ASHIGARU]</color> Took {amount} Damage! Health: {currentHealth}/{maxHealth}");

        // Instantly alert Ashigaru to combat if struck while unaware
        awareness = 100f;
        hasBeenAlerted = true;

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            TriggerDirectionalHitReaction(knockbackDir);
        }
    }

    private void TriggerDirectionalHitReaction(Vector3 knockbackDir)
    {
        if (animator == null) return;

        // Determine vector where the attack originated from relative to NPC forward
        Vector3 incomingDir = -knockbackDir;
        incomingDir.y = 0;
        if (incomingDir == Vector3.zero) return;
        incomingDir.Normalize();

        float angle = Vector3.SignedAngle(transform.forward, incomingDir, Vector3.up);

        if (angle >= -45f && angle <= 45f)
        {
            animator.SetTrigger("HitFront");
        }
        else if (angle > 45f && angle < 135f)
        {
            animator.SetTrigger("HitRight");
        }
        else if (angle < -45f && angle > -135f)
        {
            animator.SetTrigger("HitLeft");
        }
        else
        {
            animator.SetTrigger("HitBack");
        }
    }

    private void Die()
    {
        isDead = true;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        Debug.Log("<color=red>[ASHIGARU]</color> Obliterated.");
        Destroy(gameObject, 3.5f);
    }

    private void UpdateAnimator()
    {
        if (animator != null && agent != null && agent.enabled)
        {
            float currentMoveSpeed = agent.velocity.magnitude;
            animator.SetFloat("Speed", currentMoveSpeed);
            animator.SetInteger("State", (int)currentState);

            Vector3 localVel = transform.InverseTransformDirection(agent.velocity);
            animator.SetFloat("MoveX", localVel.x);
            animator.SetFloat("MoveZ", localVel.z);
        }
    }

    private void EvaluateVisionAndAwareness()
    {
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeightOffset;
        Vector3 targetCenter = playerTarget.position + Vector3.up * 1.0f;
        Vector3 dirToPlayer = (targetCenter - eyePosition);

        float distanceToPlayer = dirToPlayer.magnitude;
        dirToPlayer.Normalize();
        isPlayerVisible = false;

        float effectiveViewAngle = (currentState == AIState.CombatEngaged || awareness >= 100f) ? 360f : viewAngle;

        if (distanceToPlayer <= detectionRadius && Vector3.Angle(transform.forward, dirToPlayer) <= effectiveViewAngle / 2f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(eyePosition, visionThickness, dirToPlayer, detectionRadius);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == this.transform || hit.transform.root == this.transform.root) continue;
                if (hit.collider.isTrigger) continue;
                if (hit.transform.CompareTag("Player") || hit.transform.root.CompareTag("Player"))
                {
                    isPlayerVisible = true;
                    lastKnownPosition = playerTarget.position;
                    hasLastKnownPosition = true;
                    isDynamicSearching = false;
                    break;
                }
                else break;
            }
        }

        if (awareness >= 100f)
        {
            lastKnownPosition = playerTarget.position;
            hasLastKnownPosition = true;
            isDynamicSearching = false;
            if (distanceToPlayer <= escapeRadius)
            {
                currentChaseTimer = chaseMemoryDuration;
            }
            else
            {
                currentChaseTimer -= Time.deltaTime;
                if (currentChaseTimer <= 0f)
                {
                    awareness = 90f;
                    huntTimer = huntDuration;
                }
            }
        }
        else
        {
            if (isPlayerVisible)
            {
                currentGraceTimer = visionLeniency;
                currentLowAwarenessTimer = lowAwarenessMemoryDuration;
                if (hasBeenAlerted || distanceToPlayer <= combatEngageRadius)
                {
                    awareness = 100f;
                    currentChaseTimer = chaseMemoryDuration;
                }
                else
                {
                    float distanceFactor = Mathf.Lerp(3.0f, 1.0f, distanceToPlayer / detectionRadius);
                    float awarenessRatio = awareness / 100f;
                    float exponentialMultiplier = 1f + (Mathf.Pow(awarenessRatio, 3f) * 6f);
                    float frameBuild = baseBuildRate * distanceFactor * exponentialMultiplier * Time.deltaTime;
                    awareness = Mathf.Clamp(awareness + frameBuild, 0f, 100f);
                    if (awareness >= 100f)
                    {
                        hasBeenAlerted = true;
                        currentChaseTimer = chaseMemoryDuration;
                    }
                }
            }
            else
            {
                if (awareness > 0f)
                {
                    if (currentGraceTimer > 0f)
                    {
                        currentGraceTimer -= Time.deltaTime;
                    }
                    else
                    {
                        if (awareness < investigateThreshold)
                        {
                            if (currentLowAwarenessTimer > 0f)
                                currentLowAwarenessTimer -= Time.deltaTime;
                            else
                                awareness -= decayRate * Time.deltaTime;
                        }
                        else if (isDynamicSearching)
                        {
                            if (hasBeenAlerted) awareness = 90f;
                            else
                            {
                                huntTimer -= Time.deltaTime;
                                if (huntTimer <= 0f) awareness -= decayRate * Time.deltaTime;
                            }
                        }
                        else awareness -= decayRate * Time.deltaTime;
                    }
                }
            }
        }

        if (awareness <= 0f && (isDynamicSearching || hasLastKnownPosition))
        {
            isDynamicSearching = false;
            hasLastKnownPosition = false;
            if (assignedPatrolRoute != null)
            {
                currentPatrolIndex = assignedPatrolRoute.GetClosestNodeIndex(transform.position);
                isWaitingAtPatrolNode = false;
            }
        }

        if (awareness >= 100f)
            currentState = (distanceToPlayer <= combatEngageRadius) ? AIState.CombatEngaged : AIState.Chasing;
        else if (awareness > 0f)
            currentState = AIState.Suspicious;
        else
            currentState = AIState.Unaware;

        if (showRuntime3DMeshes) UpdateDebugColors();
    }

    private void ExecuteStateBehaviors()
    {
        switch (currentState)
        {
            case AIState.Unaware:
            case AIState.Suspicious:
            case AIState.Chasing:
                agent.updateRotation = true;
                attackTimer = 0f;

                if (currentState == AIState.Unaware)
                {
                    if (assignedPatrolRoute != null && assignedPatrolRoute.nodes.Count > 0)
                    {
                        agent.isStopped = false;
                        agent.speed = suspiciousWalkSpeed * 0.6f;
                        agent.acceleration = suspiciousAcceleration;
                        agent.angularSpeed = suspiciousTurnSpeed;
                        if (isWaitingAtPatrolNode)
                        {
                            agent.isStopped = true;
                            patrolWaitTimer -= Time.deltaTime;
                            if (patrolWaitTimer <= 0f)
                            {
                                isWaitingAtPatrolNode = false;
                                currentPatrolIndex = (currentPatrolIndex + 1) % assignedPatrolRoute.nodes.Count;
                            }
                        }
                        else
                        {
                            Transform targetNode = assignedPatrolRoute.nodes[currentPatrolIndex].waypoint;
                            if (targetNode != null)
                            {
                                agent.SetDestination(targetNode.position);
                                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                                {
                                    isWaitingAtPatrolNode = true;
                                    patrolWaitTimer = assignedPatrolRoute.nodes[currentPatrolIndex].waitTime;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (agent.hasPath) agent.ResetPath();
                        agent.isStopped = true;
                    }
                }
                else if (currentState == AIState.Suspicious)
                {
                    if (awareness < investigateThreshold)
                    {
                        agent.isStopped = true;
                        Vector3 lookPos = isPlayerVisible ? playerTarget.position : (hasLastKnownPosition ? lastKnownPosition : transform.position);
                        Vector3 lookDir = lookPos - transform.position;
                        lookDir.y = 0;
                        if (lookDir != Vector3.zero)
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 3f);
                    }
                    else
                    {
                        agent.isStopped = false;
                        float activeWalkSpeed = hasBeenAlerted ? franticJogSpeed : suspiciousWalkSpeed;
                        float speedLerp = (awareness - investigateThreshold) / (100f - investigateThreshold);

                        agent.speed = Mathf.Lerp(activeWalkSpeed, chasingRunSpeed, speedLerp);
                        agent.acceleration = Mathf.Lerp(suspiciousAcceleration, chasingAcceleration, speedLerp);
                        agent.angularSpeed = Mathf.Lerp(suspiciousTurnSpeed, chasingTurnSpeed, speedLerp);
                        if (isPlayerVisible)
                        {
                            pathTimer += Time.deltaTime;
                            if (pathTimer >= pathUpdateTime)
                            {
                                pathTimer = 0f;
                                agent.SetDestination(playerTarget.position);
                            }
                        }
                        else if (!isDynamicSearching && hasLastKnownPosition)
                        {
                            pathTimer += Time.deltaTime;
                            if (pathTimer >= pathUpdateTime)
                            {
                                pathTimer = 0f;
                                agent.SetDestination(lastKnownPosition);
                            }
                            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                            {
                                isDynamicSearching = true;
                                searchWaitTimer = 1.0f;
                                currentSearchDestination = transform.position;
                            }
                        }
                        else if (isDynamicSearching)
                        {
                            if (searchWaitTimer > 0f)
                            {
                                agent.isStopped = true;
                                searchWaitTimer -= Time.deltaTime;
                                transform.Rotate(0, 90f * Time.deltaTime, 0);
                            }
                            else
                            {
                                agent.isStopped = false;
                                agent.SetDestination(currentSearchDestination);
                                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                                {
                                    searchWaitTimer = Random.Range(1.5f, 3.0f);
                                    currentSearchDestination = GetRandomNavMeshPoint(lastKnownPosition, searchRadius);
                                }
                            }
                        }
                    }
                }
                else if (currentState == AIState.Chasing)
                {
                    agent.isStopped = false;
                    agent.speed = chasingRunSpeed;
                    agent.acceleration = chasingAcceleration;
                    agent.angularSpeed = chasingTurnSpeed;

                    pathTimer += Time.deltaTime;
                    if (pathTimer >= pathUpdateTime)
                    {
                        pathTimer = 0f;
                        agent.SetDestination(playerTarget.position);
                    }
                }
                break;

            case AIState.CombatEngaged:
                agent.updateRotation = false;

                if (!isAttacking && !isHeavySmashing && !isCharging)
                {
                    Vector3 lookDirection = playerTarget.position - transform.position;
                    lookDirection.y = 0;
                    if (lookDirection != Vector3.zero)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDirection), Time.deltaTime * 8f);
                    }
                }

                float currentDistance = Vector3.Distance(transform.position, playerTarget.position);
                float maxThrustDistance = thrustRange + attackRangeBuffer;

                Vector3 playerVel = (playerTarget.position - prevPlayerPos) / Time.deltaTime;
                Vector3 dirToNPC = (transform.position - playerTarget.position).normalized;
                bool isPlayerAggressive = (Vector3.Dot(playerVel, dirToNPC) > 0.5f) || (currentDistance < thrustRange);

                if (postureLockTimer > 0f)
                {
                    postureLockTimer -= Time.deltaTime;
                }
                else
                {
                    if (isPlayerAggressive && currentPosture == CombatPosture.Aggressive)
                    {
                        currentPosture = CombatPosture.Defensive;
                        postureLockTimer = Random.Range(3f, 10f);
                    }
                    else if (!isPlayerAggressive && currentPosture == CombatPosture.Defensive)
                    {
                        currentPosture = CombatPosture.Aggressive;
                        postureLockTimer = Random.Range(3f, 10f);
                    }
                }

                orbitTimer -= Time.deltaTime;
                if (orbitTimer <= 0f)
                {
                    orbitDirection = (Random.value > 0.5f) ? 1 : -1;
                    orbitTimer = Random.Range(3f, 6f);
                }

                if (isHeavyHolding)
                {
                    heavyHoldTimer += Time.deltaTime;
                    if (currentDistance <= heavySmashRange || heavyHoldTimer >= heavyHoldMaxDuration)
                    {
                        ExecuteHeavySmash();
                    }
                }
                else if (!isAttacking && !isHeavySmashing && !isCharging && currentDistance >= chargeMinDistance)
                {
                    attackTimer += Time.deltaTime;
                    if (attackTimer >= attackCooldown)
                    {
                        attackTimer = 0f;
                        float chargeChance = (currentPosture == CombatPosture.Aggressive) ? chargeChanceAggressive : chargeChanceDefensive;
                        if (Random.value <= chargeChance)
                        {
                            StartCoroutine(ExecuteTwoStepChargeRoutine());
                        }
                    }
                }
                else if (!isAttacking && !isHeavySmashing && !isCharging && currentDistance <= maxThrustDistance)
                {
                    attackTimer += Time.deltaTime;
                    if (attackTimer >= attackCooldown)
                    {
                        attackTimer = 0f;
                        float heavyChance = (currentPosture == CombatPosture.Defensive) ? heavyChanceDefensive : heavyChanceAggressive;
                        if (Random.value <= heavyChance)
                        {
                            StartHeavyWindup();
                        }
                        else
                        {
                            TriggerThrustAttack();
                        }
                    }
                }
                else if (!isAttacking && !isHeavyHolding && !isHeavySmashing && !isCharging)
                {
                    attackTimer = Mathf.Min(attackTimer + Time.deltaTime, attackCooldown - 0.2f);
                }

                if (isAttacking || isHeavySmashing || isCharging)
                {
                    agent.isStopped = true;
                }
                else
                {
                    agent.isStopped = false;
                    Vector3 moveIntent = Vector3.zero;
                    moveIntent += transform.right * orbitDirection;

                    if (isHeavyHolding)
                    {
                        moveIntent += transform.forward;
                        agent.speed = combatAdvanceSpeed * 0.8f;
                    }
                    else if (currentPosture == CombatPosture.Defensive)
                    {
                        if (isPlayerAggressive && retreatTimer <= 0f)
                        {
                            retreatTimer = Random.Range(1f, 3f);
                        }

                        if (retreatTimer > 0f)
                        {
                            retreatTimer -= Time.deltaTime;
                            moveIntent -= transform.forward;
                            agent.speed = combatBackstepSpeed;
                        }
                        else
                        {
                            agent.speed = combatOrbitSpeed;
                        }
                    }
                    else
                    {
                        if (currentDistance > maxThrustDistance)
                        {
                            moveIntent += transform.forward;
                            agent.speed = combatAdvanceSpeed;
                        }
                        else
                        {
                            agent.speed = combatOrbitSpeed;
                        }
                    }

                    if (moveIntent != Vector3.zero)
                    {
                        agent.SetDestination(transform.position + moveIntent.normalized * 2.0f);
                    }
                }
                break;
        }
    }

    private IEnumerator ExecuteTwoStepChargeRoutine()
    {
        isCharging = true;
        chargeHitDealt = false;
        agent.isStopped = true;

        if (animator != null)
        {
            animator.SetTrigger("ChargeAttack");
        }

        Vector3 chargeDirection = transform.forward;
        if (playerTarget != null)
        {
            Vector3 targetDir = (playerTarget.position - transform.position);
            targetDir.y = 0;
            if (targetDir != Vector3.zero) chargeDirection = targetDir.normalized;
        }

        float timer = 0f;
        while (timer < dash1Duration)
        {
            timer += Time.deltaTime;
            agent.Move(chargeDirection * dash1Speed * Time.deltaTime);
            yield return null;
        }

        yield return new WaitForSeconds(dashDelay);

        timer = 0f;
        while (timer < dash2Duration)
        {
            timer += Time.deltaTime;
            agent.Move(chargeDirection * dash2Speed * Time.deltaTime);

            if (!chargeHitDealt && playerTarget != null)
            {
                Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
                if (Physics.SphereCast(rayOrigin, 0.6f, transform.forward, out RaycastHit hit, 2.5f))
                {
                    if (hit.transform.CompareTag("Player") || hit.transform.root.CompareTag("Player"))
                    {
                        chargeHitDealt = true;
                        if (AdamState.Instance != null)
                        {
                            AdamState.Instance.TakeDamage(chargeAttackDamage, transform.forward);
                            Debug.Log($"<color=red>[ASHIGARU]</color> CHARGE SURGE CONNECTED for {chargeAttackDamage} damage!");
                        }
                    }
                }
            }
            yield return null;
        }

        yield return new WaitForSeconds(chargeRecoveryDuration);

        isCharging = false;
        agent.isStopped = false;
    }

    private void StartHeavyWindup()
    {
        isAttacking = true;
        if (animator != null)
        {
            animator.SetTrigger("HeavyWindup");
            animator.SetBool("IsHeavyHolding", true);
        }

        Invoke(nameof(EnterHeavyHoldState), heavyWindupDuration);
    }

    private void EnterHeavyHoldState()
    {
        isAttacking = false;
        isHeavyHolding = true;
        heavyHoldTimer = 0f;
    }

    private void ExecuteHeavySmash()
    {
        isHeavyHolding = false;
        isHeavySmashing = true;

        if (animator != null)
        {
            animator.SetBool("IsHeavyHolding", false);
            animator.SetTrigger("HeavySmash");
        }

        Invoke(nameof(ExecuteHeavySmashDamage), heavySmashWindupTime);
        Invoke(nameof(ResetHeavySmashLock), heavySmashLockDuration);
    }

    private void ResetHeavySmashLock()
    {
        isHeavySmashing = false;
    }

    private void ExecuteHeavySmashDamage()
    {
        if (playerTarget == null) return;

        Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
        Vector3 forwardDir = transform.forward;

        if (Physics.SphereCast(rayOrigin, 0.7f, forwardDir, out RaycastHit hit, heavySmashRange))
        {
            if (hit.transform.CompareTag("Player") || hit.transform.root.CompareTag("Player"))
            {
                if (AdamState.Instance != null)
                {
                    AdamState.Instance.TakeDamage(heavySmashDamage, transform.forward);
                    Debug.Log($"<color=red>[ASHIGARU]</color> HEAVY SMASH HIT Adam for {heavySmashDamage} damage!");
                }
            }
        }
    }

    private void TriggerThrustAttack()
    {
        isAttacking = true;

        if (animator != null)
        {
            animator.SetTrigger("Thrust");
        }

        Invoke(nameof(ExecuteThrustDamage), attackWindupTime);
        Invoke(nameof(ResetAttackLock), attackLockDuration);
    }

    private void ResetAttackLock()
    {
        isAttacking = false;
    }

    private void ExecuteThrustDamage()
    {
        if (playerTarget == null) return;

        Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
        Vector3 forwardDir = transform.forward;

        if (Physics.SphereCast(rayOrigin, 0.5f, forwardDir, out RaycastHit hit, thrustRange))
        {
            if (hit.transform.CompareTag("Player") || hit.transform.root.CompareTag("Player"))
            {
                if (AdamState.Instance != null)
                {
                    AdamState.Instance.TakeDamage(attackDamage, transform.forward);
                    Debug.Log($"<color=red>[ASHIGARU]</color> Thrust Attack Hit Adam for {attackDamage} damage!");
                }
            }
        }
    }

    private Vector3 GetRandomNavMeshPoint(Vector3 center, float radius)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * radius;
            randomDirection += center;
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        return center;
    }

    private void UpdateDebugColors()
    {
        if (coneMat == null || combatMat == null || escapeMat == null) return;
        Color targetConeColor = new Color(0.0f, 1.0f, 0.2f, 0.15f);
        Color targetCombatColor = new Color(1.0f, 0.1f, 0.1f, 0.1f);
        switch (currentState)
        {
            case AIState.Unaware:
                break;
            case AIState.Suspicious:
                Color suspiciousYellow = new Color(1.0f, 0.8f, 0.0f, 0.30f);
                targetConeColor = Color.Lerp(new Color(0.0f, 1.0f, 0.2f, 0.15f), suspiciousYellow, awareness / 100f);
                break;
            case AIState.Chasing:
                targetConeColor = new Color(1.0f, 0.4f, 0.0f, 0.4f);
                break;
            case AIState.CombatEngaged:
                targetConeColor = new Color(1.0f, 0.4f, 0.0f, 0.15f);
                targetCombatColor = new Color(1.0f, 0.0f, 0.0f, 0.6f);
                break;
        }
        if (awareness >= 100f) escapeMat.SetColor("_BaseColor", new Color(1.0f, 0.0f, 0.0f, 0.15f));
        else escapeMat.SetColor("_BaseColor", new Color(1.0f, 1.0f, 1.0f, 0.03f));
        coneMat.SetColor("_BaseColor", targetConeColor);
        combatMat.SetColor("_BaseColor", targetCombatColor);
    }

    #region --- GIZMOS & PROCEDURAL GEOMETRY ---
    private void OnDrawGizmos()
    {
        if (!showEditorGizmos) return;
        if (Application.isPlaying)
        {
            switch (currentState)
            {
                case AIState.Unaware: Gizmos.color = Color.green; break;
                case AIState.Suspicious: Gizmos.color = Color.Lerp(Color.green, Color.yellow, awareness / 100f); break;
                case AIState.Chasing: Gizmos.color = new Color(1.0f, 0.4f, 0.0f); break;
                case AIState.CombatEngaged: Gizmos.color = Color.red; break;
            }
            if (isDynamicSearching)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Gizmos.DrawWireSphere(lastKnownPosition, searchRadius);
            }
        }
        else Gizmos.color = new Color(0.0f, 0.8f, 0.4f, 0.5f);

        Vector3 eyePos = transform.position + Vector3.up * eyeHeightOffset;
        Gizmos.DrawSphere(eyePos, 0.15f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.0f, transform.forward * thrustRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, heavySmashRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, chargeMinDistance);

        Gizmos.color = Application.isPlaying && awareness >= 100f ? new Color(1.0f, 0.0f, 0.0f, 0.5f) : new Color(1.0f, 1.0f, 1.0f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, escapeRadius);
        Gizmos.color = Application.isPlaying ? Gizmos.color : new Color(0.0f, 0.8f, 0.4f, 0.5f);

        float drawAngle = (Application.isPlaying && (currentState == AIState.CombatEngaged || awareness >= 100f)) ? 360f : viewAngle;
        Vector3 leftRay = Quaternion.Euler(0, -drawAngle / 2f, 0) * transform.forward;
        Vector3 rightRay = Quaternion.Euler(0, drawAngle / 2f, 0) * transform.forward;
        Gizmos.DrawLine(eyePos, eyePos + leftRay * detectionRadius);
        Gizmos.DrawLine(eyePos, eyePos + rightRay * detectionRadius);

        int arcSegments = 16;
        Vector3 lastArcPoint = eyePos + leftRay * detectionRadius;
        float startAngle = -drawAngle / 2f;
        float angleStep = drawAngle / arcSegments;
        for (int i = 1; i <= arcSegments; i++)
        {
            Vector3 nextDir = Quaternion.Euler(0, startAngle + (angleStep * i), 0) * transform.forward;
            Vector3 nextArcPoint = eyePos + nextDir * detectionRadius;
            Gizmos.DrawLine(lastArcPoint, nextArcPoint);
            lastArcPoint = nextArcPoint;
        }

        Gizmos.color = Application.isPlaying && currentState == AIState.CombatEngaged ? Color.red : new Color(1.0f, 0.3f, 0.3f, 0.4f);
        int circleSegments = 24;
        float circleStep = 360f / circleSegments;
        Vector3 lastCirclePoint = transform.position + transform.forward * combatEngageRadius;
        for (int i = 1; i <= circleSegments; i++)
        {
            Vector3 nextDir = Quaternion.Euler(0, circleStep * i, 0) * transform.forward;
            Vector3 nextCirclePoint = transform.position + nextDir * combatEngageRadius;
            Gizmos.DrawLine(lastCirclePoint, nextCirclePoint);
            lastCirclePoint = nextCirclePoint;
        }

        Gizmos.color = Color.cyan;
        Vector3 boxCenter = hitBoxOffset != null ? hitBoxOffset.position : transform.position + new Vector3(0, hitBoxDimensions.y / 2f, 0);
        Gizmos.DrawWireCube(boxCenter, hitBoxDimensions);
    }

    private void CreateDebugGeometry()
    {
        debugContainer = new GameObject("[AI_Debug_Rig]");
        debugContainer.transform.SetParent(transform, false);
        escapeVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        escapeVisual.transform.SetParent(debugContainer.transform, false);
        escapeVisual.transform.localPosition = new Vector3(0, 0.005f, 0);
        escapeMat = CreateURPTransparentMaterial(new Color(1.0f, 1.0f, 1.0f, 0.03f));
        PreparePrimitiveChild(escapeVisual, debugContainer.transform, escapeMat);
        combatVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        combatVisual.transform.SetParent(debugContainer.transform, false);
        combatVisual.transform.localPosition = new Vector3(0, 0.01f, 0);
        combatMat = CreateURPTransparentMaterial(new Color(1.0f, 0.1f, 0.1f, 0.10f));
        PreparePrimitiveChild(combatVisual, debugContainer.transform, combatMat);
        coneVisual = new GameObject("Procedural_Vision_Cone");
        coneVisual.transform.SetParent(debugContainer.transform, false);
        coneVisual.transform.localPosition = new Vector3(0, 0.02f, 0);
        coneMeshFilter = coneVisual.AddComponent<MeshFilter>();
        MeshRenderer coneRenderer = coneVisual.AddComponent<MeshRenderer>();
        coneMat = CreateURPTransparentMaterial(new Color(0.0f, 1.0f, 0.2f, 0.15f));
        coneRenderer.material = coneMat;
        coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coneRenderer.receiveShadows = false;
        hitboxVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        PreparePrimitiveChild(hitboxVisual, debugContainer.transform, CreateURPTransparentMaterial(new Color(0.0f, 0.8f, 1.0f, 0.3f)));
        UpdateGeometryTransforms();
        UpdateDebugColors();
    }

    private void PreparePrimitiveChild(GameObject obj, Transform parent, Material targetMat)
    {
        if (obj.TryGetComponent<Collider>(out Collider col)) Destroy(col);
        if (obj.TryGetComponent<MeshRenderer>(out MeshRenderer ren))
        {
            ren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ren.receiveShadows = false;
            ren.material = targetMat;
        }
    }

    private void UpdateGeometryTransforms()
    {
        if (escapeVisual != null) escapeVisual.transform.localScale = new Vector3(escapeRadius * 2f, 0.001f, escapeRadius * 2f);
        if (combatVisual != null) combatVisual.transform.localScale = new Vector3(combatEngageRadius * 2f, 0.002f, combatEngageRadius * 2f);
        if (hitboxVisual != null)
        {
            hitboxVisual.transform.localScale = hitBoxDimensions;
            hitboxVisual.transform.position = hitBoxOffset != null ? hitBoxOffset.position : transform.position + new Vector3(0, hitBoxDimensions.y / 2f, 0);
        }
        if (coneMeshFilter != null) coneMeshFilter.mesh = GenerateWedgeMesh();
    }

    private Mesh GenerateWedgeMesh()
    {
        Mesh mesh = new Mesh();
        int segments = 32;
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;

        float drawAngle = (Application.isPlaying && (currentState == AIState.CombatEngaged || awareness >= 100f)) ? 360f : viewAngle;
        float startAngle = -drawAngle / 2f;
        float angleIncrement = drawAngle / segments;
        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = startAngle + (angleIncrement * i);
            float rad = currentAngle * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(rad) * detectionRadius, 0, Mathf.Cos(rad) * detectionRadius);
        }
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private void ToggleDebugGeometry(bool state)
    {
        if (state)
        {
            if (debugContainer == null) CreateDebugGeometry();
            debugContainer.SetActive(true);
        }
        else if (debugContainer != null) debugContainer.SetActive(false);
    }

    private Material CreateURPTransparentMaterial(Color targetColor)
    {
        Shader urpUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (urpUnlitShader == null) urpUnlitShader = Shader.Find("Internal-Colored");
        Material mat = new Material(urpUnlitShader);
        mat.SetColor("_BaseColor", targetColor);
        mat.SetFloat("_Surface", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    private void OnDestroy()
    {
        if (debugContainer != null) Destroy(debugContainer);
    }
    #endregion
}