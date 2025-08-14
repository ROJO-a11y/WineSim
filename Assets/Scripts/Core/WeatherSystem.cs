using UnityEngine;

public struct DailyWeather
{
    public float temperature; // °C
    public float rainfall;    // mm
    public float sun;         // 0..1
}

public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem I { get; private set; }
    public DailyWeather Today { get; private set; }

    [SerializeField] private GameConfig cfg;
    private System.Random rng;

    void Awake() { I = this; rng = new System.Random(); }

    public void TickDaily(int dayOfYear)
    {
        // Simple sine for temp/sun across "year"
        float t = (float)dayOfYear / cfg.daysPerYear; // 0..1
        float season = Mathf.Sin((t * Mathf.PI * 2f) - Mathf.PI / 2f) * 0.5f + 0.5f; // 0..1

        float baseTemp = Mathf.Lerp(5f, 30f, season);        // winter→summer
        float tempNoise = (float)(rng.NextDouble() * 6f - 3f);
        float temperature = baseTemp + tempNoise;

        float rainChance = Mathf.Lerp(0.15f, 0.35f, 1f - season); // more rain off-summer
        float rainfall = (rng.NextDouble() < rainChance) ? (float)(rng.NextDouble() * 12f) : 0f;

        float sun = Mathf.Clamp01(Mathf.Lerp(0.4f, 0.9f, season) + (float)(rng.NextDouble() * 0.2f - 0.1f));

        Today = new DailyWeather { temperature = temperature, rainfall = rainfall, sun = sun };
    }
}