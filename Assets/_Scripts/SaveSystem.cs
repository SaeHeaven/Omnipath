using UnityEngine;
using System.IO;
using System.Collections.Generic;

// RESTORED: The data containers Unity needs to write to the hard drive
[System.Serializable]
public class OutpostSaveData
{
    public string outpostID;
    public int statusState; 
}

[System.Serializable]
public class GameSaveProfile
{
    public List<OutpostSaveData> savedOutposts = new List<OutpostSaveData>();
    public bool angelsInvaded = false;
    public bool tundraWarActive = false;
}

public static class SaveSystem
{
    private static string savePath = Path.Combine(Application.persistentDataPath, "project_adam_save.json");

    public static void SaveGame(List<Outpost> liveOutposts, bool angelsInvaded, bool tundraWarActive)
    {
        GameSaveProfile profile = new GameSaveProfile();

        foreach (Outpost outpost in liveOutposts)
        {
            OutpostSaveData data = new OutpostSaveData();
            data.outpostID = outpost.outpostName;
            data.statusState = (int)outpost.currentState;
            profile.savedOutposts.Add(data);
        }

        profile.angelsInvaded = angelsInvaded;
        profile.tundraWarActive = tundraWarActive;

        string jsonText = JsonUtility.ToJson(profile, true);
        File.WriteAllText(savePath, jsonText);
        Debug.Log($"💾 WORLD STATE RECORDED! Choice state written to: {savePath}");
    }

    public static GameSaveProfile LoadGame()
    {
        if (!File.Exists(savePath))
        {
            Debug.LogWarning("🔍 No save file detected. Clean campaign initialized.");
            return null; 
        }

        string jsonText = File.ReadAllText(savePath);
        GameSaveProfile profile = JsonUtility.FromJson<GameSaveProfile>(jsonText);
        return profile;
    }

    public static void WipeSaveData()
    {
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
            Debug.Log("🗑️ SAVE WIPED: The hard drive save file has been completely deleted.");
        }
    }
}