using UnityEngine;
using UnityEngine.AI;
using TMPro; // Necessary inclusion to drive textual UI canvas references
using UnityEngine.InputSystem; // Access New Input System direct hardware queries

[RequireComponent(typeof(NavMeshAgent))]
public class YuriBoss : MonoBehaviour
{
    public enum BossState { Intro, Stalking, DefensiveStance, MakarovAim, BladeSweep, Defeated }
    
    [Header("State Control")]
    public BossState currentState = BossState.Stalking;
    private NavMeshAgent agent;
    private CharacterController playerController;
    private Transform playerTransform;

    [Header("Combat Metrics")]
    public float maxHealth = 150f;
    private float currentHealth;
    public float actionCooldown = 2.5f;
    private float actionTimer = 0f;

    [Header("Telegraph Timers")]
    public float aimDuration = 1.2f;
    public float defensiveDuration = 2.0f;
    private float stateTimer = 0f;

    [Header("Destiny Choice Links")]
    private TextMeshProUGUI choiceTextUI;
    public float interactionRange = 5f;

    private Renderer meshRenderer;
    private Color originalColor;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        currentHealth = maxHealth;
        meshRenderer = GetComponent<Renderer>();
        if (meshRenderer != null) originalColor = meshRenderer.material.color;
    }

    private void Start()
    {
        // FIXED: Unity 6 modern standard
        AdamController player = FindFirstObjectByType<AdamController>();
        if (player != null)
        {
            playerTransform = player.transform;
            playerController = player.GetComponent<CharacterController>();
        }

        GameObject choiceObj = GameObject.Find("ChoiceText");
        if (choiceObj != null)
        {
            choiceTextUI = choiceObj.GetComponent<TextMeshProUGUI>();
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        if (currentState == BossState.Defeated)
        {
            HandleDestinyChoiceInput();
            return;
        }

        Vector3 lookDir = playerTransform.position - transform.position;
        lookDir.y = 0;
        if (lookDir.magnitude > 0.1f) transform.rotation = Quaternion.LookRotation(lookDir);

        HandleStateExecution();
    }

    private void HandleStateExecution()
    {
        switch (currentState)
        {
            case BossState.Stalking:
                agent.isStopped = false;
                agent.SetDestination(playerTransform.position);
                
                actionTimer += Time.deltaTime;
                if (actionTimer >= actionCooldown) SelectNextAttack();
                break;

            case BossState.DefensiveStance:
                agent.isStopped = true;
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f) ResetToStalking();
                break;

            case BossState.MakarovAim:
                agent.isStopped = true;
                stateTimer -= Time.deltaTime;
                if (meshRenderer != null) meshRenderer.material.color = Color.yellow;
                if (stateTimer <= 0f) ExecuteMakarovShot();
                break;

            case BossState.BladeSweep:
                break;
        }
    }

    private void SelectNextAttack()
    {
        actionTimer = 0f;
        int choice = Random.Range(0, 3);

        if (choice == 0)
        {
            currentState = BossState.DefensiveStance;
            stateTimer = defensiveDuration;
            if (meshRenderer != null) meshRenderer.material.color = Color.blue;
            Debug.Log("🛡️ Yuri enters a Defensive Stance!");
        }
        else if (choice == 1)
        {
            currentState = BossState.MakarovAim;
            stateTimer = aimDuration;
            Debug.Log("🔫 Yuri draws his Makarov!");
        }
        else
        {
            ExecuteBladeSweep();
        }
    }

    private void ExecuteMakarovShot()
    {
        Debug.Log("💥 Yuri fires his Makarov!");
        if (Vector3.Distance(transform.position, playerTransform.position) <= 30f)
        {
            if (!playerController.isGrounded)
            {
                Debug.LogWarning("❌ CRITICAL HIT! Adam knocked out of sky!");
            }
        }
        ResetToStalking();
    }

    private void ExecuteBladeSweep()
    {
        currentState = BossState.BladeSweep;
        Debug.Log("⚔️ Yuri executes a wide low Blade Sweep!");

        if (Vector3.Distance(transform.position, playerTransform.position) <= 4.5f)
        {
            if (playerController.height >= 1.0f)
            {
                Debug.LogError("💥 Adam hit by sweep strike!");
                AdamState.Instance.TakeDamage(25f);
            }
        }
        ResetToStalking();
    }

    private void ResetToStalking()
    {
        if (meshRenderer != null) meshRenderer.material.color = originalColor;
        currentState = BossState.Stalking;
    }

    public void TakeBossDamage(float amount)
    {
        if (currentState == BossState.Defeated) return;

        if (currentState == BossState.DefensiveStance)
        {
            Debug.LogWarning("🛡️ CLANG! Frontal shield parry.");
            return;
        }

        currentHealth -= amount;
        if (currentHealth <= 0f) ExecuteDefeatSeppuku();
    }

    private void ExecuteDefeatSeppuku()
    {
        currentState = BossState.Defeated;
        agent.isStopped = true;
        if (agent.enabled) agent.enabled = false;
        
        transform.localScale = new Vector3(2f, 0.8f, 2f);
        if (meshRenderer != null) meshRenderer.material.color = Color.white;

        Debug.Log("🌸 YURI DEFEATED. Seppuku stance active. Choose his ultimate fate.");
    }

    // --- NEW METHOD: Monitors proximity and updates the split paths data ---
    private void HandleDestinyChoiceInput()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= interactionRange)
        {
            if (choiceTextUI != null)
            {
                choiceTextUI.text = "[E] RUIN YURI (Claim Wanyudo Katana)\n[R] PARTAKE (Spare Yuri & Unchain Endgame War)";
            }

            // Read the hardware interface inputs directly via the modern Unity Input System
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                ResolveFate(true);
            }
            else if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResolveFate(false);
            }
        }
        else
        {
            if (choiceTextUI != null) choiceTextUI.text = ""; // Clear text if player walks away
        }
    }

    private void ResolveFate(bool chosenRuin)
    {
        if (choiceTextUI != null) choiceTextUI.text = ""; // Wipe display canvas layers

        if (chosenRuin)
        {
            Debug.LogWarning("💀 CHOSEN PATH: RUIN. You brutally executed Yuri and claimed Wanyudo. Angels will now invade future realms!");
            GameManager.Instance.angelsInvaded = true;
            GameManager.Instance.tundraWarActive = false;
        }
        else
        {
            Debug.Log("🌸 CHOSEN PATH: PARTAKE. You honored Yuri and left him alive. The infinite Tundra End-Game defense loops are active.");
            GameManager.Instance.angelsInvaded = false;
            GameManager.Instance.tundraWarActive = true;
        }

        // Commit choice immediately across persistent storage arrays
        GameManager.Instance.RecordWorldChanges();

        // Destroys the physical boss unit avatar as the choice loop settles
        Destroy(gameObject);
    }
}