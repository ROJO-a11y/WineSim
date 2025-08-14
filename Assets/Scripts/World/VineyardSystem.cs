using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages vineyard plots, growth, readiness and harvest.
/// Exposes helper methods used by UI (GetReadiness01, IsPlanted, GetState).
/// </summary>
public class VineyardSystem : MonoBehaviour
{
    public static VineyardSystem I { get; private set; }

    [SerializeField] private GameConfig cfg;
    [SerializeField] private GrapeVarietySO[] varieties; // available varieties
    public GrapeVarietySO[] Varieties => varieties;

    // World state (rows*cols)
    public VineyardTileState[] Tiles { get; private set; }

    void Awake() { I = this; }

    // ---------------------------------------------------------------------
    // Lifecycle / Save-Load
    // ---------------------------------------------------------------------
    public void InitNewGame()
    {
        int count = Mathf.Max(1, cfg.vineyardCols * cfg.vineyardRows);
        Tiles = new VineyardTileState[count];
        for (int i = 0; i < Tiles.Length; i++)
            Tiles[i] = new VineyardTileState();

        // Start with 2 owned plots (index 0 and 1)
        if (Tiles.Length > 0) Tiles[0].owned = true;
        if (Tiles.Length > 1) Tiles[1].owned = true;
    }

    public void LoadFromSave(SaveData s)
    {
        Tiles = s.vineyardTiles != null && s.vineyardTiles.Length > 0
            ? s.vineyardTiles
            : new VineyardTileState[Mathf.Max(1, cfg.vineyardCols * cfg.vineyardRows)];
    }

    public VineyardTileState[] Serialize() => Tiles;

    // ---------------------------------------------------------------------
    // Player actions
    // ---------------------------------------------------------------------
    public bool TryBuyPlot(int index)
    {
        if (!InRange(index)) return false;
        var t = Tiles[index];
        if (t.owned) return false;
        if (!EconomySystem.I.TrySpend(cfg.landBasePrice)) return false;
        t.owned = true;
        return true;
    }

    public bool TryPlant(int index, string varietyName)
    {
        if (!InRange(index)) return false;
        var t = Tiles[index];
        if (!t.owned || !string.IsNullOrEmpty(t.plantedVariety)) return false;
        if (!EconomySystem.I.TrySpend(cfg.plantCostPerPlot)) return false;

        // Ensure variety exists
        var v = GetVariety(varietyName);
        if (v == null) return false;

        t.plantedVariety = varietyName;
        t.daysSincePlanting = 0;
        t.brix = 12f;          // early-season baseline
        t.pH = 3.2f;
        t.phenolic = 10f;
        t.yieldKg = cfg.yieldPerPlotKg;
        t.vintageYear = TimeController.I != null ? TimeController.I.Year : 0;
        return true;
    }

    // ---------------------------------------------------------------------
    // Simulation
    // ---------------------------------------------------------------------
    public void TickDaily()
    {
        var w = WeatherSystem.I != null ? WeatherSystem.I.Today : default;
        int dayOfYear = TimeController.I != null ? TimeController.I.DayOfYear : 0;
        int daysPerYear = Mathf.Max(1, cfg.daysPerYear);

        for (int i = 0; i < Tiles.Length; i++)
        {
            var t = Tiles[i];
            if (string.IsNullOrEmpty(t.plantedVariety)) continue;

            t.daysSincePlanting++;

            var variety = GetVariety(t.plantedVariety);
            if (variety == null) continue;

            // Seasonal curve 0..1 (peak mid-summer)
            float season01 = Mathf.Sin(((float)dayOfYear / daysPerYear * Mathf.PI * 2f) - Mathf.PI / 2f) * 0.5f + 0.5f;

            // Brix growth (reduced by rain)
            float brixGain = Mathf.Lerp(cfg.summerBrixPerDayMin, cfg.summerBrixPerDayMax, season01);
            if (w.rainfall > 0f) brixGain -= cfg.rainBrixPenalty;
            brixGain = Mathf.Max(0.01f, brixGain);
            t.brix = Mathf.Max(0f, t.brix + brixGain);

            // pH nudges toward variety target
            t.pH = Mathf.Lerp(t.pH, variety.targetpH, Mathf.Clamp01(cfg.pHDailyDeltaTowardsTarget));

            // Phenolic ripeness rises with warmth/sun
            float tempFactor = Mathf.Clamp01((w.temperature - 10f) / 20f); // 0 at 10°C, ~1 at 30°C
            float phenGain = cfg.phenolicDailyGain * tempFactor * Mathf.Clamp01(w.sun);
            t.phenolic = Mathf.Clamp(t.phenolic + phenGain, 0f, 100f);
        }
    }

    // ---------------------------------------------------------------------
    // Readiness & Harvest
    // ---------------------------------------------------------------------
    public bool CanHarvest(int index, out float readiness)
    {
        readiness = GetReadiness01(index);
        return readiness >= 0.65f; // threshold window
    }

    /// <summary>0..1 readiness score used by the tile UI bar.</summary>
    public float GetReadiness01(int index)
    {
        if (!InRange(index)) return 0f;
        var t = Tiles[index];
        if (string.IsNullOrEmpty(t.plantedVariety)) return 0f;
        var variety = GetVariety(t.plantedVariety);
        if (variety == null) return 0f;

        // Brix: centered around target ±3
        float brixScore = Mathf.InverseLerp(variety.targetHarvestBrix - 3f, variety.targetHarvestBrix + 3f, t.brix);
        // Phenolics: from min threshold → 100
        float phenScore = Mathf.InverseLerp(variety.minHarvestPhenolic - 10f, 100f, t.phenolic);
        // pH: closeness to target (±0.6 → 0)
        float pHScore = 1f - Mathf.Clamp01(Mathf.Abs(t.pH - variety.targetpH) / 0.6f);

        // Weighted readiness
        float r = brixScore * 0.5f + pHScore * 0.2f + phenScore * 0.3f;
        return Mathf.Clamp01(r);
    }

    /// <summary>
    /// Create a grape batch from a plot and reset season metrics.
    /// Caller (ActionAPI/ProductionSystem) decides whether it fits a tank or sells wholesale.
    /// </summary>
    public GrapeBatch Harvest(int index)
    {
        if (!InRange(index)) return null;
        var t = Tiles[index];
        var variety = GetVariety(t.plantedVariety);
        if (variety == null) return null;

        var batch = new GrapeBatch
        {
            varietyName = t.plantedVariety,
            vintageYear = t.vintageYear,
            brix = t.brix,
            pH = t.pH,
            phenolic = t.phenolic,
            kg = Mathf.RoundToInt(t.yieldKg)
        };

        // Reset plot metrics for next season (vine remains planted)
        t.daysSincePlanting = 0;
        t.brix = 12f; t.pH = 3.2f; t.phenolic = 10f;
        t.yieldKg = cfg.yieldPerPlotKg;
        t.vintageYear = TimeController.I != null ? TimeController.I.Year : t.vintageYear;

        return batch;
    }

    // ---------------------------------------------------------------------
    // UI helpers
    // ---------------------------------------------------------------------
    public bool IsPlanted(int index) => InRange(index) && !string.IsNullOrEmpty(Tiles[index].plantedVariety);
    public VineyardTileState GetState(int index) => InRange(index) ? Tiles[index] : null;

    // ---------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------
    private bool InRange(int i) => Tiles != null && i >= 0 && i < Tiles.Length;
    private GrapeVarietySO GetVariety(string name) => System.Array.Find(varieties, v => v.varietyName == name);
}