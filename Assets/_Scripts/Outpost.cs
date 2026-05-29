using UnityEngine;
using System.Collections.Generic;

public class Outpost : MonoBehaviour
{
    public enum OutpostState { Contested, PlayerSecured }

    [Header("Identity Details")]
    public string outpostName = "Outpost Alpha";
    public EnemyBrain.Faction originalOwnerFaction = EnemyBrain.Faction.Wrath;
    public OutpostState currentState = OutpostState.Contested;

    [Header("Defenders Spawn Setup")]
    public GameObject enemyPrefab;       
    public int totalDefenders = 3;       
    public float spawnRadius = 8f; // INCREASED: Pushes them safely away from the cube walls

    private List<EnemyBrain> activeDefenders = new List<EnemyBrain>();

    private void Start()
    {
        if (currentState == OutpostState.Contested)
        {
            SpawnOutpostGuards();
        }
        else
        {
            ForceSecureVisuals(); 
        }
    }

    private void SpawnOutpostGuards()
    {
        if (enemyPrefab == null) return;

        for (int i = 0; i < totalDefenders; i++)
        {
            // THE FIX: Adding .normalized forces the guards to spawn exactly on the outer ring of the radius!
            Vector2 randomCircle = Random.insideUnitCircle.normalized * spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, 0.5f, randomCircle.y);

            GameObject guardObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            guardObj.name = $"{outpostName}_Guard_{i+1}";

            EnemyBrain brain = guardObj.GetComponent<EnemyBrain>();
            if (brain != null)
            {
                brain.enemyFaction = originalOwnerFaction;
                brain.AssignToOutpost(this);
                activeDefenders.Add(brain);
            }
        }
    }

    public void DefenderKilled(EnemyBrain fallenDefender)
    {
        if (activeDefenders.Contains(fallenDefender))
        {
            activeDefenders.Remove(fallenDefender);
        }

        if (activeDefenders.Count == 0 && currentState == OutpostState.Contested)
        {
            CaptureOutpost();
        }
    }

    private void CaptureOutpost()
    {
        currentState = OutpostState.PlayerSecured;
        ForceSecureVisuals();
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecordWorldChanges();
        }
    }

    public void ForceSecureVisuals()
    {
        Renderer baseRenderer = GetComponent<Renderer>();
        if (baseRenderer != null)
        {
            baseRenderer.material.color = Color.green;
        }
    }
}