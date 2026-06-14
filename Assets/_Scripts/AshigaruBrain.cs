using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AshigaruBrain : MonoBehaviour
{
    public enum AIState { Unaware, Suspicious, Chasing, CombatEngaged }

    [Header("--- CURRENT ACTIVE STATE ---")]
    [SerializeField] private AIState currentState = AIState.Unaware;
    [Range(0f, 100f)] public float awareness = 0f;
    public bool hasBeenAlerted = false;

    [Header("--- PATROL & SEARCH SYSTEM ---")]
    [Tooltip("Drag a PatrolRoute GameObject from the scene here.")]
    public PatrolRoute assignedPatrolRoute;
    [Tooltip("How wide of an area they randomly scan when they lose you.")]
    public float searchRadius = 8.0f;
    [Tooltip("How long they search a room from Yellow/Orange before giving up and returning to patrol.")]
    public float huntDuration = 15.0f;
    [Tooltip("Fast speed used when searching if they were previously in full Red alert.")]
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
    [Tooltip("How long they stare at your last spot from Green (low awareness) before going back to patrol.")]
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

    // Tracking references
    private Transform playerTarget;
    private NavMeshAgent agent;
    private float pathTimer;
    private float pathUpdateTime = 0.1f; 

    // Search & Memory State
    private Vector3 lastKnownPosition;
    private bool hasLastKnownPosition = false;
    private bool isPlayerVisible = false;
    private float currentChaseTimer = 0f;
    private float currentGraceTimer = 0f;
    private float currentLowAwarenessTimer = 0f; // NEW: Tracks the "stare down" in green zone

    // Dynamic Scanning & Patrol Trackers
    private bool isDynamicSearching = false;
    private float huntTimer = 0f;
    private float searchWaitTimer = 0f;
    private Vector3 currentSearchDestination;
    private int currentPatrolIndex = 0;
    private float patrolWaitTimer = 0f;
    private bool isWaitingAtPatrolNode = false;

    // Debug rig variables
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
        lastVisualState = showRuntime3DMeshes;
        agent.autoBraking = true; 

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) playerTarget = playerObj.transform;

        if (showRuntime3DMeshes) CreateDebugGeometry();
    }

    private void Update()
    {
        if (showRuntime3DMeshes != lastVisualState)
        {
            ToggleDebugGeometry(showRuntime3DMeshes);
            lastVisualState = showRuntime3DMeshes;
        }

        if (showRuntime3DMeshes && debugContainer != null) UpdateGeometryTransforms();

        if (playerTarget == null) return;

        EvaluateVisionAndAwareness();
        ExecuteStateBehaviors();
    }

    private void EvaluateVisionAndAwareness()
    {
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeightOffset;
        Vector3 targetCenter = playerTarget.position + Vector3.up * 1.0f; 
        Vector3 dirToPlayer = (targetCenter - eyePosition);
        
        float distanceToPlayer = dirToPlayer.magnitude;
        dirToPlayer.Normalize();

        isPlayerVisible = false;

        // Step 1: Volumetric SphereCast
        if (distanceToPlayer <= detectionRadius && Vector3.Angle(transform.forward, dirToPlayer) <= viewAngle / 2f)
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

        // Step 2: The Core Logic Matrix
        if (awareness >= 100f)
        {
            // BUG 2 FIX: Constantly overwrite the ghost marker while fully tracking so they hunt the NEW location!
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
                    // Predatory Chase broke. Drop awareness to 90 to trigger the Frantic Hunt.
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
                currentLowAwarenessTimer = lowAwarenessMemoryDuration; // Keep the stare timer topped off

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
                        // BUG 1 FIX: Green Zone Decay logic implemented properly
                        if (awareness < investigateThreshold)
                        {
                            // Let them stare at the wall for 3 seconds, then naturally decay
                            if (currentLowAwarenessTimer > 0f)
                            {
                                currentLowAwarenessTimer -= Time.deltaTime;
                            }
                            else
                            {
                                awareness -= decayRate * Time.deltaTime;
                            }
                        }
                        else if (!isDynamicSearching && hasLastKnownPosition)
                        {
                            // Yellow/Orange: Do not decay while physically walking to the ghost marker!
                        }
                        else if (isDynamicSearching)
                        {
                            if (hasBeenAlerted)
                            {
                                // Permanent Alert: Lock awareness at 90f. Infinite search.
                                awareness = 90f; 
                            }
                            else
                            {
                                // Suspicious Alert: Run the timer down, then decay awareness.
                                huntTimer -= Time.deltaTime;
                                if (huntTimer <= 0f)
                                {
                                    awareness -= decayRate * Time.deltaTime;
                                }
                            }
                        }
                        else
                        {
                            awareness -= decayRate * Time.deltaTime;
                        }
                    }
                }
            }
        }

        // Clean up Patrol Data when Awareness completely resets
        if (awareness <= 0f && (isDynamicSearching || hasLastKnownPosition))
        {
            isDynamicSearching = false;
            hasLastKnownPosition = false;
            if (assignedPatrolRoute != null)
            {
                // Snap to the closest node and resume patrol!
                currentPatrolIndex = assignedPatrolRoute.GetClosestNodeIndex(transform.position);
                isWaitingAtPatrolNode = false;
            }
        }

        // Step 3: State Mapping
        if (awareness >= 100f)
        {
            currentState = (distanceToPlayer <= combatEngageRadius) ? AIState.CombatEngaged : AIState.Chasing;
        }
        else if (awareness > 0f)
        {
            currentState = AIState.Suspicious;
        }
        else
        {
            currentState = AIState.Unaware;
        }

        if (showRuntime3DMeshes) UpdateDebugColors();
    }

    private void ExecuteStateBehaviors()
    {
        switch (currentState)
        {
            case AIState.Unaware:
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
                break;

            case AIState.Suspicious:
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
                break;

            case AIState.Chasing:
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
                break;

            case AIState.CombatEngaged:
                agent.isStopped = true;
                Vector3 lookDirection = playerTarget.position - transform.position;
                lookDirection.y = 0;
                if (lookDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDirection), Time.deltaTime * 8f);
                }
                break;
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

        Gizmos.color = Application.isPlaying && awareness >= 100f ? new Color(1.0f, 0.0f, 0.0f, 0.5f) : new Color(1.0f, 1.0f, 1.0f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, escapeRadius);

        Gizmos.color = Application.isPlaying ? Gizmos.color : new Color(0.0f, 0.8f, 0.4f, 0.5f);
        Vector3 leftRay = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
        Vector3 rightRay = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;
        Gizmos.DrawLine(eyePos, eyePos + leftRay * detectionRadius);
        Gizmos.DrawLine(eyePos, eyePos + rightRay * detectionRadius);

        int arcSegments = 16;
        Vector3 lastArcPoint = eyePos + leftRay * detectionRadius;
        float startAngle = -viewAngle / 2f;
        float angleStep = viewAngle / arcSegments;

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

        float startAngle = -viewAngle / 2f;
        float angleIncrement = viewAngle / segments;

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