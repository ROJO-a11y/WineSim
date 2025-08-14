using UnityEngine;
using System;
using System.IO;

[Serializable]
public class SaveData
{
    public int day;
    public DateTime utcSavedAt;

    // Economy
    public int cash;
    public float marketIndex;
    public float brandLevel;

    // Vineyard
    public VineyardTileState[] vineyardTiles;

    // Production
    public TankState[] tanks;
    public BarrelState[] barrels;
    public bool hasBottlingMachine;

    // Inventory
    public BottleEntryState[] inventory;

    public static SaveData FromSystems(int day)
    {
        return new SaveData
        {
            day = day,
            utcSavedAt = DateTime.UtcNow,
            cash = EconomySystem.I.Cash,
            marketIndex = EconomySystem.I.MarketIndex,
            brandLevel = EconomySystem.I.BrandLevel,
            vineyardTiles = VineyardSystem.I.Serialize(),
            tanks = ProductionSystem.I.SerializeTanks(),
            barrels = ProductionSystem.I.SerializeBarrels(),
            hasBottlingMachine = ProductionSystem.I.HasBottlingMachine,
            inventory = InventorySystem.I.Serialize()
        };
    }
}

public static class SaveLoadManager
{
    private static string PathFile => Path.Combine(Application.persistentDataPath, "save.json");

    /// <summary>
    /// Returns true if a save file currently exists.
    /// </summary>
    public static bool HasSave() => File.Exists(PathFile);

    /// <summary>
    /// Write the save file to persistentDataPath. Wrapped in try/catch for safety.
    /// </summary>
    public static void Save(SaveData data)
    {
        try
        {
            var json = JsonUtility.ToJson(data); // set prettyPrint=true if you want readable files
            File.WriteAllText(PathFile, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"SaveLoadManager.Save failed: {e.Message}\nPath: {PathFile}");
        }
    }

    /// <summary>
    /// Load the save file from persistentDataPath; returns null if missing or invalid.
    /// </summary>
    public static SaveData Load()
    {
        if (!File.Exists(PathFile)) return null;
        try
        {
            var json = File.ReadAllText(PathFile);
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveLoadManager.Load failed: {e.Message}\nPath: {PathFile}");
            return null;
        }
    }

    /// <summary>
    /// Delete the save file (and clear any legacy PlayerPrefs keys), safe to call even if no file exists.
    /// </summary>
    public static void Delete()
    {
        try
        {
            if (File.Exists(PathFile)) File.Delete(PathFile);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveLoadManager.Delete failed: {e.Message}\nPath: {PathFile}");
        }

        // Legacy/defensive cleanup if any PlayerPrefs were used for saving in the past
        PlayerPrefs.DeleteKey("WineSim_Save");
        PlayerPrefs.Save();
    }
}