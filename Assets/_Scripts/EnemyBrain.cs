using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBrain : MonoBehaviour, IDamageable // <-- Added IDamageable here so the new guns work!
{
    public enum Faction { Angel, Wrath }

    [Header("Faction Settings")]
    public Faction enemyFaction = Faction.Wrath;
    private Outpost assignedOutpost; 

    [Header("Elite & Honor Configurations")]
    public bool isFortressGuard = false;
    public bool standsDownWithHonor = false; 

    private NavMeshAgent agent;

    [Header("Combat Stats")]
    public float maxHealth = 10f;
    private float currentHealth;

    [Header("Movement Tweaks")]
    public float chaseSpeed = 4.5f;

    [Header("Stagger Configuration")]
    public float staggerDuration = 0.35f; 
    private float staggerTimer = 0f;
    private bool isStaggered = false;

    [Header("Proxy War Radar (New System)")]
    public float detectionRadius = 15f;
    public float radarPulseRate = 0.5f;
    private float radarTimer = 0f;
    private Transform activeTarget; 

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        currentHealth = maxHealth;
        agent.speed = chaseSpeed; 
    }

    private void Start()
    {
        // We removed the code that forced them to hunt Adam permanently.
        // The Radar handles it now!
    }

    // Allows the Outpost script to connect to this unit
    public void AssignToOutpost(Outpost outpost)
    {
        assignedOutpost = outpost;
    }

    private void Update()
    {
        if (currentHealth <= 0f) return;
        if (agent == null || !agent.enabled) return;

        if (isStaggered)
        {
            staggerTimer -= Time.deltaTime;
            if (staggerTimer <= 0f)
            {
                isStaggered = false;
                agent.isStopped = false; 
            }
            return; 
        }

        // --- THE RADAR PULSE SYSTEM ---
        if (activeTarget == null)
        {
            radarTimer -= Time.deltaTime;
            if (radarTimer <= 0f)
            {
                ScanForTargets();
                radarTimer = radarPulseRate;
            }
        }
        else
        {
            // Tunnel Vision: Chase the active target once found
            agent.SetDestination(activeTarget.position);
        }
    }

    private void ScanForTargets()
    {
        // Send out an invisible sphere to find objects
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius);
        Transform potentialPlayer = null;
        Transform potentialEnemy = null;

        foreach (Collider hit in hits)
        {
            // CRITICAL: Make sure the Adam GameObject has the tag "Player" in the Unity Editor!
            if (hit.CompareTag("Player")) potentialPlayer = hit.transform;
            
            EnemyBrain otherAI = hit.GetComponent<EnemyBrain>();
            if (otherAI != null && otherAI.enemyFaction != this.enemyFaction && otherAI.currentHealth > 0)
            {
                potentialEnemy = hit.transform;
            }
        }

        // The Logic Coin Flip
        if (potentialPlayer != null && potentialEnemy != null)
        {
            // 50/50 chance to pick either the player or the enemy faction
            activeTarget = (Random.Range(1, 101) <= 50) ? potentialPlayer : potentialEnemy;
        }
        else if (potentialPlayer != null) activeTarget = potentialPlayer;
        else if (potentialEnemy != null) activeTarget = potentialEnemy;
    }

    public void TakeDamage(float damageAmount, Vector3 knockbackDirection)
    {
        currentHealth -= damageAmount;
        Debug.Log($"🩸 {enemyFaction} Unit Hit! Health: {currentHealth}/{maxHealth}");

        if (standsDownWithHonor)
        {
            standsDownWithHonor = false;
            agent.isStopped = false;
            Debug.LogWarning("⚠️ HONOR BROKEN! The Wrath Guardians have broken their silence because you struck them!");
        }

        isStaggered = true;
        staggerTimer = staggerDuration;
        agent.isStopped = true; 

        transform.position += knockbackDirection.normalized * 0.5f;

        if (currentHealth <= 0f)
        {
            Debug.Log($"💀 {enemyFaction} Unit Obliterated.");
            
            if (assignedOutpost != null)
            {
                assignedOutpost.DefenderKilled(this);
            }
            
            Destroy(gameObject, 0.1f);
        }
    }
}