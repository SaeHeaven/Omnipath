using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBrain : MonoBehaviour
{
    public enum Faction { Angel, Wrath }

    [Header("Faction Settings")]
    public Faction enemyFaction = Faction.Wrath;
    private Outpost assignedOutpost; 

    [Header("Elite & Honor Configurations")]
    public bool isFortressGuard = false;
    public bool standsDownWithHonor = false; // If true, AI will not attack Adam out of respect

    private NavMeshAgent agent;
    private Transform playerTarget;

    [Header("Combat Stats")]
    public float maxHealth = 10f;
    private float currentHealth;

    [Header("Movement Tweaks")]
    public float chaseSpeed = 4.5f;

    [Header("Stagger Configuration")]
    public float staggerDuration = 0.35f; 
    private float staggerTimer = 0f;
    private bool isStaggered = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        currentHealth = maxHealth;
        agent.speed = chaseSpeed; 
    }

    private void Start()
    {
        AdamController playerScript = FindFirstObjectByType<AdamController>();
        if (playerScript != null)
        {
            playerTarget = playerScript.transform;
        }

        // SYSTEMIC RULE: If this is a Max Alert Fortress Guard, turn them into a giant Mini-Boss!
        if (isFortressGuard && !standsDownWithHonor)
        {
            transform.localScale = new Vector3(2f, 2.5f, 2f); // Double their physical scale!
            maxHealth = 40f; // Quadruple their health pool
            currentHealth = maxHealth;
            agent.speed = 6f; // Make them faster and more terrifying
        }
        // If they stand down with honor, they still spawn at normal/large size but frozen
        else if (isFortressGuard && standsDownWithHonor)
        {
            transform.localScale = new Vector3(2f, 2.5f, 2f); // Still look imposing!
            agent.isStopped = true; // Stop their pathfinding wheel drivers completely
        }
    }

    public void AssignToOutpost(Outpost outpost)
    {
        assignedOutpost = outpost;
    }

    private void Update()
    {
        // HONOR CHECK: If standing down out of respect, do absolutely nothing
        if (standsDownWithHonor)
        {
            if (agent.enabled) agent.isStopped = true;
            return;
        }

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

        if (playerTarget != null)
        {
            agent.SetDestination(playerTarget.position);
        }
    }

    public void TakeDamage(float damageAmount, Vector3 knockbackDirection)
    {
        currentHealth -= damageAmount;
        Debug.Log($"🩸 {enemyFaction} Unit Hit! Health: {currentHealth}/{maxHealth}");

        // Honorable guards will retaliate ONLY if Adam acts dishonorably by attacking them first!
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
            Destroy(gameObject); 
        }
    }
}