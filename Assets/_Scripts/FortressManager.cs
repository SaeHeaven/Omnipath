using UnityEngine;
using System.Collections.Generic;

public class FortressManager : MonoBehaviour
{
    [Header("Guard Prefab Configuration")]
    public GameObject enemyPrefab; // Drag your TargetAI prefab file here
    public int guardCount = 4;      // Spawns 4 elite gatekeepers
    public float spawnRadius = 12f;

    private List<Outpost> totalOutposts = new List<Outpost>();

    private void Start()
    {
        // 1. Gather all outposts currently deployed on the map grid
        totalOutposts.AddRange(FindObjectsByType<Outpost>(FindObjectsSortMode.None));

        // 2. Check how many ANGEL outposts are still active and unbeaten
        int activeAngelBases = 0;
        foreach (Outpost op in totalOutposts)
        {
            if (op.originalOwnerFaction == EnemyBrain.Faction.Angel && op.currentState == Outpost.OutpostState.Contested)
            {
                activeAngelBases++;
            }
        }

        // 3. Determine Honor/Alert Status
        // If ALL angel outposts are secured, the alert drops to Low out of respect!
        bool lowAlertHonorState = (activeAngelBases == 0);

        if (lowAlertHonorState)
        {
            Debug.Log("🌸 LOW ALERT: The Angels are broken. The Unresting Spirits stand down out of honor.");
        }
        else
        {
            Debug.LogWarning($"🔥 MAX ALERT! {activeAngelBases} Angel encampments remain operational. Prepare for Elite mini-boss defense layouts!");
        }

        // 4. Generate the Gatekeepers
        SpawnFortressSentinels(lowAlertHonorState);
    }

    private void SpawnFortressSentinels(bool standDown)
    {
        for (int i = 0; i < guardCount; i++)
        {
            // Calculate a circular perimeter around the cold-iron walls
            float angle = i * Mathf.PI * 2 / guardCount;
            Vector3 spawnOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
            Vector3 spawnPos = transform.position + spawnOffset;
            spawnPos.y = 1f; // Snap their feet securely to the baked floor plane

            GameObject guardObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            guardObj.name = $"Fortress_Elite_Sentinel_{i+1}";

            EnemyBrain brain = guardObj.GetComponent<EnemyBrain>();
            if (brain != null)
            {
                brain.enemyFaction = EnemyBrain.Faction.Wrath; // They belong to the Lord of Wrath
                brain.isFortressGuard = true;                  // Activate their elite status properties
                brain.standsDownWithHonor = standDown;         // Inject the state calculated from the war front
            }
        }
    }
}