using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-200)]

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

    // Resolve active config (prefer holder, fallback to serialized field)
    private GameConfig Cfg() => GameConfigHolder.Instance ? GameConfigHolder.Instance.Config : cfg;

    // Ensure the Tiles array exists and all elements are instantiated
    private void EnsureTilesAllocated()
    {
        var c = Cfg();
        int rows = (c != null && c.vineyardRows > 0) ? c.vineyardRows : 6;
        int cols = (c != null && c.vineyardCols > 0) ? c.vineyardCols : 4;
        int count = Mathf.Max(1, rows * cols);

        if (Tiles == null || Tiles.Length != count)
            Tiles = new VineyardTileState[count];

        for (int i = 0; i < Tiles.Length; i++)
            if (Tiles[i] == null)
                Tiles[i] = new VineyardTileState();
    }

    // Compute a calendar vintage year = GameConfig.startYear + TimeController.Year (with guards)
    private int GetCalendarYear()
    {
        var c = Cfg();
        int baseYear = (c != null) ? c.startYear : 0;
        int yearIdx  = (TimeController.I != null) ? TimeController.I.Year : 0;
        int calendar = (baseYear > 0 ? baseYear : 0) + yearIdx;
        if (calendar <= 0) calendar = yearIdx;
        return calendar;
    }

    void Awake()
    {
        I = this;
        EnsureTilesAllocated();
        Debug.Log($"VineyardSystem: Tiles allocated? {(Tiles != null ? Tiles.Length : 0)}");

    }

    

    // ---------------------------------------------------------------------
    // Lifecycle / Save-Load
    // ---------------------------------------------------------------------
    public void InitNewGame()
    {
        EnsureTilesAllocated();

        var c = Cfg();
        float lpk = (c != null && c.defaultLitersPerKg > 0f) ? c.defaultLitersPerKg : 0.70f;

        for (int i = 0; i < Tiles.Length; i++)
        {
            var t = Tiles[i];
            t.owned = false;
            t.plantedVariety = string.Empty;
            t.daysSincePlanting = 0;
            t.brix = 12f;
            t.pH = 3.2f;
            t.phenolic = 10f;
            t.yieldKg = (c != null ? c.yieldPerPlotKg : 1200);
            t.vintageYear = GetCalendarYear();
            t.soilMoisture01 = 0.5f;
            t.litersPerKg = lpk;
            t.TA = 7.0f;
            t.waterStress01 = 0f;
            t.diseasePressure01 = 0f;
            t.colorIndex = 0f;
            t.aromaIndex = 0f;
        }

        // Start with 2 owned plots (index 0 and 1)
        if (Tiles.Length > 0) Tiles[0].owned = true;
        if (Tiles.Length > 1) Tiles[1].owned = true;
    }

    public void LoadFromSave(SaveData s)
    {
        if (s != null && s.vineyardTiles != null && s.vineyardTiles.Length > 0)
        {
            Tiles = s.vineyardTiles;
            for (int i = 0; i < Tiles.Length; i++)
                if (Tiles[i] == null) Tiles[i] = new VineyardTileState();
        }
        else
        {
            EnsureTilesAllocated();
        }
    }

    public VineyardTileState[] Serialize() => Tiles;

    // ---------------------------------------------------------------------
    // Player actions
    // ---------------------------------------------------------------------
    public bool TryBuyPlot(int index)
    {
        EnsureTilesAllocated();

        if (!InRange(index)) return false;
        var t = Tiles[index];
        if (t.owned) return false;
        if (!EconomySystem.I.TrySpend(cfg.landBasePrice)) return false;
        t.owned = true;
        return true;
    }

    public bool TryPlant(int index, string varietyName)
    {
        EnsureTilesAllocated();

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
        t.vintageYear = GetCalendarYear();
        return true;
    }

    // ---------------------------------------------------------------------
    // Simulation
    // ---------------------------------------------------------------------
    public void TickDaily()
    {
        var cfgLocal = Cfg();
        if (cfgLocal == null)
        {
            Debug.LogError("VineyardSystem.TickDaily: GameConfig is null. Ensure a GameConfigHolder with a valid asset is in the scene.", this);
            return;
        }

        if (Tiles == null || Tiles.Length == 0)
        {
            Debug.LogWarning("VineyardSystem.TickDaily: Tiles array is null/empty.", this);
            return;
        }

        DailyWeather w = WeatherSystem.I != null ? WeatherSystem.I.Today() : default(DailyWeather);
        int dayOfYear = TimeController.I != null ? TimeController.I.DayOfYear : 0;
        int daysPerYear = Mathf.Max(1, cfgLocal.daysPerYear);
        float defaultLpk = (cfgLocal.defaultLitersPerKg > 0f) ? cfgLocal.defaultLitersPerKg : 0.70f;

        for (int i = 0; i < Tiles.Length; i++)
        {
            if (Tiles[i] == null) Tiles[i] = new VineyardTileState();
            var t = Tiles[i];
            if (string.IsNullOrEmpty(t.plantedVariety)) continue;

            // --- Advanced weather-driven physiology ---
            t.soilMoisture01 = Mathf.Clamp01(t.soilMoisture01 + w.rainMm / 50f - w.evapoTranspirationMm / 30f);
            float vpdStress = Mathf.Clamp01(w.vpdKPa / 2f);
            t.waterStress01 = Mathf.Clamp01(0.5f * vpdStress + 0.5f * Mathf.InverseLerp(0.35f, 0.05f, t.soilMoisture01));
            t.diseasePressure01 = Mathf.Clamp01(t.diseasePressure01 + (w.mildewIndex / 100f) * 0.02f - 0.01f);

            if (t.TA <= 0f) t.TA = 7.0f;
            float acidLoss = 0.015f * Mathf.InverseLerp(10f, 30f, w.tAvgC);
            if (w.heatwave) acidLoss *= 1.25f;
            t.TA = Mathf.Max(2.5f, t.TA - acidLoss);

            if (t.litersPerKg <= 0f) t.litersPerKg = defaultLpk;
            float hydration = Mathf.InverseLerp(0.2f, 0.9f, t.soilMoisture01);
            t.litersPerKg = Mathf.Clamp01(Mathf.Lerp(0.66f, 0.74f, hydration));

            t.daysSincePlanting++;

            var variety = GetVariety(t.plantedVariety);
            if (variety == null)
            {
                Debug.LogWarning($"VineyardSystem.TickDaily: Variety '{t.plantedVariety}' not found; skipping tile {i}.", this);
                continue;
            }

            // Seasonal curve 0..1 (peak mid-summer)
            float season01 = Mathf.Sin(((float)dayOfYear / daysPerYear * Mathf.PI * 2f) - Mathf.PI / 2f) * 0.5f + 0.5f;

            // Brix growth (reduced by rain)
            float brixGain = Mathf.Lerp(cfgLocal.summerBrixPerDayMin, cfgLocal.summerBrixPerDayMax, season01);
            if (w.rainMm > 0.01f) brixGain -= cfgLocal.rainBrixPenalty;
            if (w.rainMm > 10f && t.brix >= variety.targetHarvestBrix - 2f)
                brixGain *= 0.6f; // dilution slows sugar gain near harvest
            brixGain = Mathf.Max(0.01f, brixGain);
            t.brix = Mathf.Max(0f, t.brix + brixGain);

            // pH nudges toward variety target + gentle drift from acid loss
            t.pH = Mathf.Lerp(t.pH, variety.targetpH, Mathf.Clamp01(cfgLocal.pHDailyDeltaTowardsTarget));
            t.pH = Mathf.Clamp(t.pH + acidLoss * 0.10f, 2.8f, 4.2f);

            // Phenolic ripeness rises with warmth/sun and modified by stress and disease
            float tempFactor = Mathf.Clamp01((w.tAvgC - 10f) / 20f);
            float sunFac = Mathf.InverseLerp(2f, 14f, w.sunHours);
            float phenGain = cfgLocal.phenolicDailyGain * tempFactor * sunFac * (1f + 0.25f * t.waterStress01) * (1f - 0.30f * t.diseasePressure01);
            t.phenolic = Mathf.Clamp(t.phenolic + phenGain, 0f, 100f);

            t.colorIndex = Mathf.Clamp(t.colorIndex + 0.05f * sunFac * (1f + 0.2f * t.waterStress01) - 0.03f * (w.rainMm > 5f ? 1f : 0f), 0f, 100f);
            t.aromaIndex = Mathf.Clamp(t.aromaIndex + 0.04f * Mathf.InverseLerp(12f, 22f, w.tAvgC) * (1f - 0.2f * t.waterStress01) - 0.02f * (w.heatwave ? 1f : 0f), 0f, 100f);
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

        // Neem de jaarwaarde direct van de tijdlijn (fallback: wat er op de tile stond)
        int harvestYear = GetCalendarYear();

        var today = WeatherSystem.I != null ? WeatherSystem.I.Today() : default(DailyWeather);
        var batch = new GrapeBatch
        {
            varietyName = t.plantedVariety,
            vintageYear = harvestYear,
            brix = t.brix,
            pH = t.pH,
            phenolic = t.phenolic,
            kg = Mathf.RoundToInt(t.yieldKg),

            // advanced composition
            TA = t.TA,
            YAN = 120f + 80f * Mathf.InverseLerp(0.25f, 0.8f, t.soilMoisture01) - 40f * t.diseasePressure01,
            waterStress01 = t.waterStress01,
            diseasePressure01 = t.diseasePressure01,
            colorIndex = t.colorIndex,
            aromaIndex = t.aromaIndex,
            grapeTempC = today.tAvgC,
            dilution01 = Mathf.Clamp01(Mathf.InverseLerp(0.2f, 0.9f, t.soilMoisture01) * Mathf.InverseLerp(0f, 15f, today.rainMm))
        };

        // Reset plot voor volgend seizoen
        t.daysSincePlanting = 0;
        t.brix = 12f; t.pH = 3.2f; t.phenolic = 10f;
        t.yieldKg = (Cfg() != null ? Cfg().yieldPerPlotKg : 1200);

        // Optioneel: tile bijwerken naar het huidige oogstjaar
        t.vintageYear = harvestYear;

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