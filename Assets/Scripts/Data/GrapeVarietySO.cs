using UnityEngine;

[CreateAssetMenu(fileName = "Variety", menuName = "WineSim/Variety")]
public class GrapeVarietySO : ScriptableObject
{
    public string varietyName;
    public bool isRed;

    [Header("Targets")]
    public float targetHarvestBrix = 24f;
    public float targetpH = 3.6f;
    public float minHarvestPhenolic = 70f; // 0..100

    [Header("Aging (days in barrel)")]
    public int sweetSpotStart = 90;
    public int sweetSpotEnd = 150;

    [Header("Bottle Behavior (daily)")]
    public float bottleImproveDays = 120f; // reds
    public float bottleWhiteDegradeStart = 60f;

    [Header("Terroir fit bonus (0..+10)")]
    public float terroirBonus = 3f;
}