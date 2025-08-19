using System;

[Serializable]
public class VineyardTileState
{
    public bool owned;
    public string plantedVariety; // null/empty if none
    public int daysSincePlanting;
    public float brix;
    public float pH;
    public float phenolic; // 0..100
    public float yieldKg;  // remaining in current season
    public int vintageYear; // tracks current cycle
    public string soil = "Loam";
    public string orientation = "South";

    // Advanced viticulture state
    public float soilMoisture01 = 0.5f;   // 0..1 soil water content proxy
    public float waterStress01 = 0f;      // 0..1 plant stress from VPD + low soil
    public float diseasePressure01 = 0f;  // 0..1 mildew/rot pressure accumulator
    public float TA = 7.0f;               // g/L titratable acidity (approx)
    public float colorIndex = 0f;         // 0..100 color/anthocyanin proxy
    public float aromaIndex = 0f;         // 0..100 aroma precursor proxy
    public float litersPerKg = 0.70f;     // kg->L conversion, varies with berry hydration
}