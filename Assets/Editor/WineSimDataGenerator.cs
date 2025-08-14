#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// One-click generator for default WineSim data assets (Varieties + Yeasts)
/// Also auto-assigns them to VineyardSystem, ProductionSystem, and InventorySystem in the open scene.
/// Place this file under an `Editor/` folder.
/// Menu: Tools/WineSim/Generate Default Data (Varieties + Yeasts)
/// </summary>
public static class WineSimDataGenerator
{
    private const string RootFolder = "Assets/ScriptableObjects/WineSim";
    private const string VarietiesFolder = RootFolder + "/Varieties";
    private const string YeastsFolder   = RootFolder + "/Yeasts";

    [MenuItem("Tools/WineSim/Generate Default Data (Varieties + Yeasts)")]
    public static void Generate()
    {
        EnsureFolderPath(RootFolder);
        EnsureFolderPath(VarietiesFolder);
        EnsureFolderPath(YeastsFolder);

        // --- Varieties ---
        var cab = CreateOrUpdateVariety(
            name: "Cabernet Sauvignon",
            isRed: true,
            targetHarvestBrix: 24f,
            targetpH: 3.6f,
            minHarvestPhenolic: 75f,
            sweetSpotStart: 120,
            sweetSpotEnd: 210,
            bottleImproveDays: 150f,
            bottleWhiteDegradeStart: 0f,
            terroirBonus: 3f
        );

        var pinot = CreateOrUpdateVariety(
            name: "Pinot Noir",
            isRed: true,
            targetHarvestBrix: 23f,
            targetpH: 3.5f,
            minHarvestPhenolic: 70f,
            sweetSpotStart: 90,
            sweetSpotEnd: 150,
            bottleImproveDays: 120f,
            bottleWhiteDegradeStart: 0f,
            terroirBonus: 3f
        );

        var chard = CreateOrUpdateVariety(
            name: "Chardonnay",
            isRed: false,
            targetHarvestBrix: 22f,
            targetpH: 3.4f,
            minHarvestPhenolic: 60f,
            sweetSpotStart: 60,
            sweetSpotEnd: 120,
            bottleImproveDays: 0f,
            bottleWhiteDegradeStart: 90f,
            terroirBonus: 2f
        );

        var sauv = CreateOrUpdateVariety(
            name: "Sauvignon Blanc",
            isRed: false,
            targetHarvestBrix: 21.5f,
            targetpH: 3.3f,
            minHarvestPhenolic: 55f,
            sweetSpotStart: 45,
            sweetSpotEnd: 100,
            bottleImproveDays: 0f,
            bottleWhiteDegradeStart: 80f,
            terroirBonus: 2f
        );

        // --- Yeasts ---
        var neutral = CreateOrUpdateYeast("Neutral",  speed: 1.0f, aroma: 0.0f, toleranceABV: 15f);
        var aromatic= CreateOrUpdateYeast("Aromatic", speed: 0.9f, aroma: 3.0f, toleranceABV: 14f);
        var robust  = CreateOrUpdateYeast("Robust",   speed: 1.2f, aroma: 1.0f, toleranceABV: 16f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Auto-assign to systems in scene (if present)
        AssignToSystems(new[] { cab, pinot, chard, sauv }, new[] { neutral, aromatic, robust });

        Debug.Log(
            "WineSim: Generated default varieties & yeasts and assigned them to systems (if found).\n" +
            $"Varieties → {VarietiesFolder}\nYeasts → {YeastsFolder}"
        );
    }

    // ---------- Helpers ----------

    private static GrapeVarietySO CreateOrUpdateVariety(
        string name, bool isRed, float targetHarvestBrix, float targetpH,
        float minHarvestPhenolic, int sweetSpotStart, int sweetSpotEnd,
        float bottleImproveDays, float bottleWhiteDegradeStart, float terroirBonus)
    {
        string path = VarietiesFolder + "/" + Safe(name) + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<GrapeVarietySO>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<GrapeVarietySO>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.varietyName = name;
        asset.isRed = isRed;
        asset.targetHarvestBrix = targetHarvestBrix;
        asset.targetpH = targetpH;
        asset.minHarvestPhenolic = minHarvestPhenolic;
        asset.sweetSpotStart = sweetSpotStart;
        asset.sweetSpotEnd = sweetSpotEnd;
        asset.bottleImproveDays = bottleImproveDays;
        asset.bottleWhiteDegradeStart = bottleWhiteDegradeStart;
        asset.terroirBonus = terroirBonus;

        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static YeastSO CreateOrUpdateYeast(string name, float speed, float aroma, float toleranceABV)
    {
        string path = YeastsFolder + "/" + Safe(name) + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<YeastSO>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<YeastSO>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.yeastName = name;
        asset.speed = speed;
        asset.aroma = aroma;
        asset.toleranceABV = toleranceABV;

        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void AssignToSystems(GrapeVarietySO[] varieties, YeastSO[] yeasts)
    {
        // VineyardSystem → varieties
        var vineyard = Object.FindFirstObjectByType<VineyardSystem>();
        if (vineyard)
        {
            var so = new SerializedObject(vineyard);
            var p = so.FindProperty("varieties");
            if (p != null)
            {
                p.arraySize = varieties.Length;
                for (int i = 0; i < varieties.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = varieties[i];
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(vineyard);
            }
        }

        // ProductionSystem → varieties + yeasts
        var production = Object.FindFirstObjectByType<ProductionSystem>();
        if (production)
        {
            var so = new SerializedObject(production);
            var pv = so.FindProperty("varieties");
            var py = so.FindProperty("yeasts");
            if (pv != null)
            {
                pv.arraySize = varieties.Length;
                for (int i = 0; i < varieties.Length; i++)
                    pv.GetArrayElementAtIndex(i).objectReferenceValue = varieties[i];
            }
            if (py != null)
            {
                py.arraySize = yeasts.Length;
                for (int i = 0; i < yeasts.Length; i++)
                    py.GetArrayElementAtIndex(i).objectReferenceValue = yeasts[i];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(production);
        }

        // InventorySystem → varieties
        var inventory = Object.FindFirstObjectByType<InventorySystem>();
        if (inventory)
        {
            var so = new SerializedObject(inventory);
            var p = so.FindProperty("varieties");
            if (p != null)
            {
                p.arraySize = varieties.Length;
                for (int i = 0; i < varieties.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = varieties[i];
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(inventory);
            }
        }

        AssetDatabase.SaveAssets();
    }

    private static string Safe(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c.ToString(), "");
        return s.Replace(' ', '_');
    }

    private static void EnsureFolderPath(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif