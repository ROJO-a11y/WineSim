using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "WineSim/GameConfig")]
public class GameConfig : ScriptableObject
{

    [Header("Debug / Dev")]
    public bool resetOnPlay = false;

    [Header("World")]
    public int vineyardCols = 6;
    public int vineyardRows = 4;
    public int productionCols = 6;
    public int productionRows = 4;
    public int inventoryCols = 6;
    public int inventoryRows = 4;

    [Header("Economy")]
    public int startingCash = 50000;
    public int landBasePrice = 3000;       // per plot
    public int plantCostPerPlot = 800;     // planting vines
    public int smallTankPrice = 5000;
    public int medTankPrice = 12000;
    public int largeTankPrice = 25000;
    public int barrelPrice = 900;          // 225 L
    public int bottlingMachinePrice = 10000;
    public float grapeWholesalePerKg = 1.2f;

    [Header("Market")]
    public float marketIndexMin = 0.8f;
    public float marketIndexMax = 1.2f;
    public float marketStepMax = 0.01f; // per day random walk

    [Header("Time")]
    public float secondsPerDay = 2f;       // you chose 2s/day
    public int daysPerYear = 360;          // arcade-y year
    public int offlineCatchupCapDays = 30; // max 1 month

    [Header("Vineyard Growth")]
    public float summerBrixPerDayMin = 0.10f;
    public float summerBrixPerDayMax = 0.40f;
    public float rainBrixPenalty = 0.15f;      // reduces brix gain
    public float yieldPerPlotKg = 1200f;       // arcade value for a small plot
    public float pHDailyDeltaTowardsTarget = 0.02f; // nudges toward target
    public float phenolicDailyGain = 0.8f;     // generic phenolic ripeness pace

    [Header("Fermentation")]
    public int minFermentDays = 7;
    public int maxFermentDays = 14;
    public float brixToAlcohol = 0.55f;        // ABV ≈ Brix × 0.55

    [Header("Aging")]
    public int barrelVolumeL = 225;            // fixed for v1
    public float barrelOakGainPerDay = 0.15f;  // aroma/complexity (abstract)
    public float overUnderPenaltyPerDay = 0.2f;

    [Header("Bottling")]
    public float bottleImprovementPerDay = 0.02f; // small drift for reds
    public float bottleWhiteDegradePerDay = 0.01f; // slower degrade
}