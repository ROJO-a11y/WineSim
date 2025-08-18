using UnityEngine;

[CreateAssetMenu(menuName = "WineSim/Weather Config", fileName = "WeatherConfig")]
public class WeatherConfig : ScriptableObject
{
    [Header("Seasonal baselines (0..1 over the year)")]
    public AnimationCurve tempYear;   // outputs °C average
    public AnimationCurve rainYear;   // mm/day baseline
    public AnimationCurve humYear;    // % RH
    public AnimationCurve solarYear;  // MJ/m²/day
    public AnimationCurve windYear;   // kph baseline

    [Header("Noise / variability")]
    [Range(0f, 1f)] public float dayNoise = 0.35f;

    [Header("Events (per-day chance)")]
    [Range(0f, 0.2f)] public float frostProbSpring = 0.02f;
    [Range(0f, 0.2f)] public float frostProbAutumn = 0.01f;
    [Range(0f, 0.2f)] public float heatwaveProbSummer = 0.015f;
    [Range(0f, 0.2f)] public float hailProb = 0.002f;
    [Range(0f, 0.2f)] public float stormProb = 0.01f;

    [Header("Coefficients")]
    [Tooltip("Scaling for ET0 calculation (empirical).")]
    public float et0Coef = 0.8f;

    [Header("Mildew model")]
    [Range(0, 100)] public float mildewHumThresh = 85f;
    public Vector2 mildewTempBand = new Vector2(18f, 26f);
}