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
}