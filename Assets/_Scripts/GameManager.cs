using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Debugging Tools")]
    public bool wipeSaveOnStart = false;

    [Header("Global Campaign Flags")]
    public bool angelsInvaded = false;
    public bool tundraWarActive = false;

    private List<Outpost> worldOutposts = new List<Outpost>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (wipeSaveOnStart)
        {
            SaveSystem.WipeSaveData();
            wipeSaveOnStart = false; 
        }

        // FIXED: Unity 6 optimized search method
        worldOutposts.AddRange(FindObjectsByType<Outpost>(FindObjectsSortMode.None));
        Debug.Log($"[GameManager] Tracking {worldOutposts.Count} battlefront sectors.");

        ApplySaveProfile();
    }

    public void RecordWorldChanges()
    {
        SaveSystem.SaveGame(worldOutposts, angelsInvaded, tundraWarActive);
    }

    private void ApplySaveProfile()
    {
        GameSaveProfile saveProfile = SaveSystem.LoadGame();
        
        if (saveProfile == null) return; 

        angelsInvaded = saveProfile.angelsInvaded;
        tundraWarActive = saveProfile.tundraWarActive;

        Debug.Log($"📂 LOAD COMPLETED: Angels Invaded = {angelsInvaded} | Tundra War Active = {tundraWarActive}");

        foreach (Outpost liveOutpost in worldOutposts)
        {
            OutpostSaveData savedData = saveProfile.savedOutposts.Find(x => x.outpostID == liveOutpost.outpostName);
            
            if (savedData != null)
            {
                liveOutpost.currentState = (Outpost.OutpostState)savedData.statusState;

                if (liveOutpost.currentState == Outpost.OutpostState.PlayerSecured)
                {
                    liveOutpost.ForceSecureVisuals();
                }
            }
        }
    }
}