using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AshigaruBrain : MonoBehaviour
{
    public enum AIState { Unaware, Suspicious, Chasing, CombatEngaged }

    [Header("--- CURRENT ACTIVE STATE ---")]
    [SerializeField] private AIState currentState = AIState.Unaware;
    [Range(0f, 100f)]
    [Tooltip("Live tracker of how much the AI notices the player.")]
    public float awareness = 0f;

    [Header("--- AWARENESS CONFIGURATION ---")]
    [Tooltip("Base rate of awareness gain per second when at the edge of vision.")]
    public float baseBuildRate = 8.0f;
    [Tooltip("Rate of awareness decay per second when the player hides (must be lower than build rate).")]
    public float decayRate = 3.0f;

    [Header("--- MOVEMENT DYNAMICS ---")]
    [Tooltip("Awareness must pass this value before the AI actually leaves its post to investigate.")]
    [Range(10f, 90f)] public float investigateThreshold = 40.0f;
    [Tooltip("Minimum speed when the AI first starts walking to investigate.")]
    public float suspiciousWalkSpeed = 2.5f;
    [Tooltip("Maximum sprint speed when awareness hits 100 and they fully lock on.")]
    public float chasingRunSpeed = 7.0f;

    [Header("--- DETECTION CONFIGURATION ---")]
    public float detectionRadius = 15.0f; 
    [Range(10f, 180f)] public float viewAngle = 90.0f;
    public float combatEngageRadius = 5.0f; 
    public float eyeHeightOffset = 1.5f;

    [Header("--- DEBUG VISUALIZATION ---")]
    public bool showRuntime3DMeshes = true;
    public bool showEditorGizmos = true;
    public Vector3 hitBoxDimensions = new Vector3(1.2f, 1.8f, 1.2f);
    public Transform hitBoxOffset;

    private Transform playerTarget;
    private NavMeshAgent agent;
    private float pathTimer;
    private float pathUpdateTime = 0.2f;

    private GameObject debugContainer;
    private GameObject coneVisual;
    private GameObject combatVisual;
    private GameObject hitboxVisual;
    private MeshFilter coneMeshFilter;
    private Material coneMat;
    private Material combatMat;
    private bool lastVisualState;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lastVisualState = showRuntime3DMeshes;

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

        bool hasLineOfSight = false;

        // Step 1: Geometric Validation & X-Ray Penetration Cast
        if (distanceToPlayer <= detectionRadius && Vector3.Angle(transform.forward, dirToPlayer) <= viewAngle / 2f)
        {
            RaycastHit[] hits = Physics.RaycastAll(eyePosition, dirToPlayer, detectionRadius);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == this.transform || hit.transform.root == this.transform.root) continue;
                if (hit.collider.isTrigger) continue;

                if (hit.transform.CompareTag("Player") || hit.transform.root.CompareTag("Player"))
                {
                    hasLineOfSight = true;
                    break; 
                }
                else break; 
            }
        }

        // Step 2: Awareness Math Engine
        if (hasLineOfSight)
        {
            if (distanceToPlayer <= combatEngageRadius)
            {
                awareness = 100f; // Instant aggro zone
            }
            else if (awareness < 100f)
            {
                float distanceFactor = Mathf.Lerp(3.0f, 1.0f, distanceToPlayer / detectionRadius);
                float awarenessRatio = awareness / 100f;
                float exponentialMultiplier = 1f + (Mathf.Pow(awarenessRatio, 3f) * 6f); 

                float frameBuild = baseBuildRate * distanceFactor * exponentialMultiplier * Time.deltaTime;
                awareness = Mathf.Clamp(awareness + frameBuild, 0f, 100f);
            }
        }
        else
        {
            if (awareness > 0f && awareness < 100f)
            {
                awareness -= decayRate * Time.deltaTime;
                awareness = Mathf.Clamp(awareness, 0f, 100f);
            }
        }

        // Step 3: Map Awareness directly to State Machine
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
                if (agent.hasPath) agent.ResetPath();
                break;

            case AIState.Suspicious:
                if (awareness < investigateThreshold)
                {
                    // Phase 1: Noticing. Frozen but watching.
                    agent.isStopped = true;
                    
                    // Slowly rotate head/body towards the noise to telegraph suspicion
                    Vector3 lookDir = playerTarget.position - transform.position;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 3f);
                    }
                }
                else
                {
                    // Phase 2: Investigating. Threshold broken, start walking.
                    agent.isStopped = false;
                    
                    // Normalize the remaining awareness to scale speed dynamically
                    // Ex: At awareness 40, speed is 2.5. At awareness 99, speed is close to 7.0.
                    float speedLerp = (awareness - investigateThreshold) / (100f - investigateThreshold);
                    agent.speed = Mathf.Lerp(suspiciousWalkSpeed, chasingRunSpeed, speedLerp);

                    pathTimer += Time.deltaTime;
                    if (pathTimer >= pathUpdateTime)
                    {
                        pathTimer = 0f;
                        agent.SetDestination(playerTarget.position);
                    }
                }
                break;

            case AIState.Chasing:
                agent.speed = chasingRunSpeed;
                pathTimer += Time.deltaTime;
                if (pathTimer >= pathUpdateTime)
                {
                    pathTimer = 0f;
                    agent.SetDestination(playerTarget.position);
                    agent.isStopped = false;
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

    private void UpdateDebugColors()
    {
        if (coneMat == null || combatMat == null) return;

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
        }
        else Gizmos.color = new Color(0.0f, 0.8f, 0.4f, 0.5f); 

        Vector3 eyePos = transform.position + Vector3.up * eyeHeightOffset;
        Gizmos.DrawSphere(eyePos, 0.15f);

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

        coneVisual = new GameObject("Procedural_Vision_Cone");
        coneVisual.transform.SetParent(debugContainer.transform, false);
        coneVisual.transform.localPosition = new Vector3(0, 0.02f, 0); 
        coneMeshFilter = coneVisual.AddComponent<MeshFilter>();
        MeshRenderer coneRenderer = coneVisual.AddComponent<MeshRenderer>();
        coneMat = CreateURPTransparentMaterial(new Color(0.0f, 1.0f, 0.2f, 0.15f));
        coneRenderer.material = coneMat;
        coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coneRenderer.receiveShadows = false;

        combatVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        combatMat = CreateURPTransparentMaterial(new Color(1.0f, 0.1f, 0.1f, 0.10f));
        PreparePrimitiveChild(combatVisual, debugContainer.transform, combatMat);

        hitboxVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        PreparePrimitiveChild(hitboxVisual, debugContainer.transform, CreateURPTransparentMaterial(new Color(0.0f, 0.8f, 1.0f, 0.3f)));

        UpdateGeometryTransforms();
        UpdateDebugColors();
    }

    private void PreparePrimitiveChild(GameObject obj, Transform parent, Material targetMat)
    {
        obj.transform.SetParent(parent, false);
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
        if (combatVisual != null) combatVisual.transform.localScale = new Vector3(combatEngageRadius * 2f, 0.01f, combatEngageRadius * 2f);
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
        int segments = 24; 
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