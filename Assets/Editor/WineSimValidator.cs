using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class WineSimValidator
{
    [MenuItem("Tools/WineSim/Validate Setup")]
    public static void Validate()
    {
        int issues = 0;

        // Config holder
        var holder = Object.FindFirstObjectByType<GameConfigHolder>();
        if (!holder) { LogErr("GameConfigHolder not found in scene."); issues++; }
        else if (!holder.Config) { LogErr("GameConfigHolder.Config is not assigned."); issues++; }
        else
        {
            var c = holder.Config;
            if (c.smallTankCapacityL <= 0 || c.medTankCapacityL <= 0 || c.largeTankCapacityL <= 0)
            { LogWarn("Tank capacities in GameConfig are zero or missing."); issues++; }
            if (c.barrelVolumeL <= 0) { LogWarn("barrelVolumeL is zero in GameConfig."); issues++; }
            if (c.bottleSizeMl <= 0) { LogWarn("bottleSizeMl is zero in GameConfig."); issues++; }
            if (c.barrelSweetSpotDaysMax <= c.barrelSweetSpotDaysMin)
            { LogWarn("Barrel sweet-spot window min/max look inverted or equal."); issues++; }
            if (c.bottleSweetSpotDaysMax <= c.bottleSweetSpotDaysMin)
            { LogWarn("Bottle sweet-spot window min/max look inverted or equal."); issues++; }
        }

        // Item prefabs sanity (find any in scene)
        var barrelUIs = Object.FindObjectsOfType<BarrelItemUI>(true);
        foreach (var ui in barrelUIs)
        {
            var so = new SerializedObject(ui);
            if (!GetRef<TMP_Text>(so, "title")) { LogWarn($"BarrelItemUI missing Title on {ui.name}"); issues++; }
            if (!GetRef<TMP_Text>(so, "info")) { LogWarn($"BarrelItemUI missing Info on {ui.name}"); issues++; }
            if (!GetRef<Button>(so, "bottleBtn")) { LogWarn($"BarrelItemUI missing BottleBtn on {ui.name}"); issues++; }
        }

        var tankUIs = Object.FindObjectsOfType<TankItemUI>(true);
        foreach (var ui in tankUIs)
        {
            var so = new SerializedObject(ui);
            if (!GetRef<TMP_Text>(so, "title")) { LogWarn($"TankItemUI missing Title on {ui.name}"); issues++; }
            if (!GetRef<TMP_Text>(so, "info")) { LogWarn($"TankItemUI missing Info on {ui.name}"); issues++; }
            if (!GetRef<Button>(so, "rackBtn")) { LogWarn($"TankItemUI missing RackBtn on {ui.name}"); issues++; }
        }

        // Time/weather handshake
        var tc = Object.FindFirstObjectByType<TimeController>();
        if (!tc) { LogWarn("TimeController not found in scene."); issues++; }
        var ws = Object.FindFirstObjectByType<WeatherSystem>();
        if (!ws) { LogWarn("WeatherSystem not found in scene."); issues++; }

        EditorUtility.DisplayDialog("WineSim Validation",
            issues == 0 ? "Looks good! No obvious issues found." :
                          $"Validation finished. Issues/warnings: {issues}\n(Check Console for details.)",
            "OK");
    }

    static bool GetRef<T>(SerializedObject so, string fieldName) where T : Object
    {
        var p = so.FindProperty(fieldName);
        return p != null && p.objectReferenceValue != null;
    }

    static void LogErr(string msg)  => Debug.LogError("WineSim Validate: " + msg);
    static void LogWarn(string msg) => Debug.LogWarning("WineSim Validate: " + msg);
}