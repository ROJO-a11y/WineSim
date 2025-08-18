using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public struct DailyWeather
{
    public int dayIndex;

    // Temperature (°C)
    public float tMinC, tMaxC, tAvgC;

    // Precipitation (mm)
    public float rainMm;

    // Humidity
    public float humidityPct;
    // Back-compat alias (some older code used .humidity)
    public float humidity => humidityPct;

    // Wind
    public float windKph;
    public float windDirDeg;

    // Radiation / clouds
    public float solarMJm2;   // MJ/m² per day
    public float sunHours;    // hours
    public float cloudPct;    // 0..1

    // Derived agronomic signals
    public float evapoTranspirationMm; // ET₀ (rough)
    public float vpdKPa;               // vapor pressure deficit
    public float gddBase10;            // growing degree days base 10 °C

    // Simple event flags
    public bool frostRisk;  // true if min near/below 0
    public bool heatwave;   // true if max very high
    public bool hail;       // piggybacks storm
    public bool storm;

    // Disease proxy
    public float mildewIndex; // 0..100
}

[DefaultExecutionOrder(-50)]
public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem I { get; private set; }

    [Header("Config (optional)")]
    public WeatherConfig cfg;      // If null, we use code defaults
    public int seed = 12345;

    [Header("State")]
    [Tooltip("Generated days for current in-game year.")]
    public List<DailyWeather> days = new List<DailyWeather>();

    [SerializeField, Tooltip("Cached year we generated for (0-based index from TimeController).")]
    private int _generatedYearIndex = int.MinValue;

    public event Action<int, DailyWeather> OnDayGenerated; // (dayIndex, weather)

    void Awake()
    {
        if (I != null && I != this)
        {
            Debug.LogWarning("Duplicate WeatherSystem destroyed.", this);
            Destroy(gameObject);
            return;
        }
        I = this;

        // Ensure we have data for the current year
        EnsureYearGenerated(CurrentYearIndex());

        // Subscribe to time (optional but nice)
        if (TimeController.I) TimeController.I.OnNewDay += HandleNewDay;
    }

    void OnDestroy()
    {
        if (TimeController.I) TimeController.I.OnNewDay -= HandleNewDay;
        if (I == this) I = null;
    }

    private void HandleNewDay()
    {
        // If the year rolled over, regenerate
        EnsureYearGenerated(CurrentYearIndex());
        var d = CurrentDayIndex();
        if (d >= 0 && d < days.Count)
            OnDayGenerated?.Invoke(d, days[d]);
    }

    // ------- Public API -------

    public DailyWeather GetWeatherForDay(int dayIndex)
    {
        var dpy = DaysPerYear();
        if (dayIndex < 0) dayIndex = 0;
        if (dayIndex >= dpy) dayIndex = dpy - 1;
        EnsureYearGenerated(CurrentYearIndex());
        return days.Count == dpy ? days[dayIndex] : default;
    }

    public DailyWeather Today()
    {
        EnsureYearGenerated(CurrentYearIndex());
        int d = CurrentDayIndex();
        if (d >= 0 && d < days.Count) return days[d];
        return default;
    }

    // ------- Generation -------

    private void EnsureYearGenerated(int yearIndex)
    {
        int dpy = DaysPerYear();
        if (_generatedYearIndex == yearIndex && days.Count == dpy) return;

        GenerateYear(yearIndex, dpy);
        _generatedYearIndex = yearIndex;
    }

    private void GenerateYear(int yearIndex, int dpy)
    {
        days.Clear();

        // Deterministic RNG per year
        var rng = new System.Random(seed ^ (yearIndex * 73856093));
        var cfgUsed = cfg; // cache

        // Defaults if no config assigned
        AnimationCurve tempYear, rainYear, humYear, solarYear, windYear;
        float dayNoise, et0Coef, frostProbSpring, frostProbAutumn, heatwaveProbSummer, hailProb, stormProb, mildewHumThresh;
        Vector2 mildewTempBand;

        if (cfgUsed)
        {
            tempYear = cfgUsed.tempYear;
            rainYear = cfgUsed.rainYear;
            humYear = cfgUsed.humYear;
            solarYear = cfgUsed.solarYear;
            windYear = cfgUsed.windYear;

            dayNoise = Mathf.Clamp01(cfgUsed.dayNoise);
            et0Coef = Mathf.Max(0.1f, cfgUsed.et0Coef);

            frostProbSpring = Mathf.Clamp01(cfgUsed.frostProbSpring);
            frostProbAutumn = Mathf.Clamp01(cfgUsed.frostProbAutumn);
            heatwaveProbSummer = Mathf.Clamp01(cfgUsed.heatwaveProbSummer);
            hailProb = Mathf.Clamp01(cfgUsed.hailProb);
            stormProb = Mathf.Clamp01(cfgUsed.stormProb);

            mildewHumThresh = Mathf.Clamp(cfgUsed.mildewHumThresh, 0, 100);
            mildewTempBand = cfgUsed.mildewTempBand;
        }
        else
        {
            // Gentle seasonal sine baselines
            tempYear = MakeSine(-2f, +22f);  // mid ~10, swing ~12
            rainYear = MakeFlat(2.5f);       // mm/day baseline
            humYear = MakeFlat(65f);        // %
            solarYear = MakeSine(6f, 18f);    // MJ/m²/day approx via hours proxy
            windYear = MakeFlat(10f);        // kph

            dayNoise = 0.35f;
            et0Coef = 0.8f;

            frostProbSpring = 0.02f;
            frostProbAutumn = 0.01f;
            heatwaveProbSummer = 0.015f;
            hailProb = 0.002f;
            stormProb = 0.01f;

            mildewHumThresh = 85f;
            mildewTempBand = new Vector2(18f, 26f);
        }

        for (int d = 0; d < dpy; d++)
        {
            float t = d / Mathf.Max(1f, (float)dpy); // 0..~1

            var w = new DailyWeather { dayIndex = d };

            // Baselines
            float baseT = EvalSafe(tempYear, t);   // °C (avg)
            float baseRain = EvalSafe(rainYear, t);   // mm
            float baseHum = EvalSafe(humYear, t);    // %
            float baseSolar = EvalSafe(solarYear, t);  // MJ/m²
            float baseWind = EvalSafe(windYear, t);   // kph

            // Noise
            float dn = Mathf.PerlinNoise(t * 6f, 0.123f) - 0.5f;
            float wn = Mathf.PerlinNoise(t * 1.2f, 0.789f) - 0.5f;
            float rj = (float)rng.NextDouble() - 0.5f;

            // Temps
            w.tAvgC = baseT + dayNoise * 5f * dn + 0.8f * rj;
            w.tMinC = w.tAvgC - (6f + 2f * dn);
            w.tMaxC = w.tAvgC + (8f + 2f * dn);

            // Rain: baseline + events
            float rainToday = Mathf.Max(0f, baseRain + Mathf.Max(0f, (float)rng.NextDouble() - 0.7f) * 20f * dayNoise);
            if (rng.NextDouble() < 0.06) rainToday += 10f * (float)rng.NextDouble(); // occasional convective burst
            w.rainMm = rainToday;

            // Humidity / wind
            w.humidityPct = Mathf.Clamp(baseHum + 10f * dn + 5f * rj, 25f, 100f);
            w.windKph = Mathf.Max(0f, baseWind + 6f * wn + 2f * rj);
            w.windDirDeg = (float)(rng.NextDouble() * 360.0);

            // Clouds / sun / solar
            w.cloudPct = Mathf.Clamp01(0.35f + 0.4f * (0.5f - dn) + 0.25f * (1f - Mathf.InverseLerp(0f, 24f, baseSolar)));
            w.sunHours = Mathf.Clamp(12f * (1f - w.cloudPct) + 2f * dn, 0f, 14.5f);
            w.solarMJm2 = Mathf.Max(1f, baseSolar * Mathf.Lerp(0.6f, 1.1f, 1f - w.cloudPct));

            // ET0 (very rough): proportional to solar & temperature
            w.evapoTranspirationMm = Mathf.Max(0f, et0Coef * (w.solarMJm2 / 5.0f) * Mathf.Lerp(0.5f, 1.3f, Mathf.InverseLerp(5f, 30f, w.tAvgC)));

            // VPD (kPa) from T & RH: es ~ 0.6108*exp(17.27*T/(T+237.3)) ; VPD = es*(1-RH/100)
            float es = 0.6108f * Mathf.Exp(17.27f * w.tAvgC / (w.tAvgC + 237.3f));
            w.vpdKPa = Mathf.Max(0f, es * (1f - w.humidityPct / 100f));

            // GDD base 10 °C
            w.gddBase10 = Mathf.Max(0f, w.tAvgC - 10f);

            // Seasons (0..1 of year)
            bool isSpring = t < 0.25f;
            bool isSummer = t >= 0.25f && t < 0.50f;
            bool isAutumn = t >= 0.50f && t < 0.75f;

            // Events
            if (isSpring && rng.NextDouble() < frostProbSpring) w.frostRisk = (w.tMinC <= 0.5f);
            if (isAutumn && rng.NextDouble() < frostProbAutumn) w.frostRisk |= (w.tMinC <= 0.5f);
            if (isSummer && rng.NextDouble() < heatwaveProbSummer) w.heatwave = (w.tMaxC >= 34f);
            if (rng.NextDouble() < hailProb) { w.hail = true; w.storm = true; }
            else if (rng.NextDouble() < stormProb) w.storm = true;

            // Mildew risk (simple): warm + humid + low VPD
            float humTerm = Mathf.InverseLerp(mildewHumThresh, 100f, w.humidityPct);
            float tempBand = Mathf.InverseLerp(mildewTempBand.x, mildewTempBand.y, w.tAvgC);
            float vpdTerm = 1f - Mathf.Clamp01(w.vpdKPa / 2.0f); // low VPD -> higher risk
            w.mildewIndex = Mathf.Clamp01(0.4f * humTerm + 0.4f * tempBand + 0.2f * vpdTerm) * 100f;

            days.Add(w);
            OnDayGenerated?.Invoke(d, w);
        }
    }

    // ------- Utilities -------

    private int CurrentYearIndex() => TimeController.I ? TimeController.I.Year : 0;
    private int CurrentDayIndex() => TimeController.I ? TimeController.I.Day : 0;
    private int DaysPerYear()
    {
        // Try to read from your TimeController if present; fall back to 360
        int dpy = 360;
        var tc = TimeController.I;
        if (tc != null)
        {
            var f = tc.GetType().GetField("DaysPerYear");
            if (f != null && f.FieldType == typeof(int)) dpy = (int)f.GetValue(tc);
            var p = tc.GetType().GetProperty("DaysPerYear");
            if (p != null && p.PropertyType == typeof(int)) dpy = (int)(p.GetValue(tc) ?? dpy);
        }
        return Mathf.Clamp(dpy, 30, 400);
    }

    private static AnimationCurve MakeSine(float min, float max)
    {
        // Simple sine-like year using 5 keys
        var c = new AnimationCurve();
        c.AddKey(new Keyframe(0f, (min + max) * 0.5f));
        c.AddKey(new Keyframe(0.25f, max));
        c.AddKey(new Keyframe(0.5f, (min + max) * 0.5f));
        c.AddKey(new Keyframe(0.75f, min));
        c.AddKey(new Keyframe(1f, (min + max) * 0.5f));
        return c;
    }
    private static AnimationCurve MakeFlat(float val)
    {
        var c = new AnimationCurve();
        c.AddKey(0f, val);
        c.AddKey(1f, val);
        return c;
    }
    private static float EvalSafe(AnimationCurve c, float t) => c == null ? 0f : c.Evaluate(t);
    
    // Back-compat shim so TimeController can advance weather explicitly each day.
    public void TickDaily()
    {
        // Call the indexed version using the current day-of-year if available
        int doy = TimeController.I ? TimeController.I.DayOfYear : 0;
        TickDaily(doy);
    }

    public void TickDaily(int dayOfYear)
    {
        // If your WeatherSystem generates data per-year, make sure it's ready.
        // (These helpers exist in the upgraded WeatherSystem I shared; if your version
        //  doesn't have them, you can omit these two lines.)
        try { 
            var mi = GetType().GetMethod("EnsureYearGenerated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi?.Invoke(this, new object[] { TimeController.I ? TimeController.I.Year : 0 });
        } catch { /* ok */ }

        // Clamp index within your days list
        int idx = Mathf.Clamp(dayOfYear, 0, (days != null && days.Count > 0) ? days.Count - 1 : 0);

        // If you expose a day-generated/day-changed event, fire it so listeners update.
        // (If you don't have such an event, the method still exists so TimeController compiles.)
        try {
            var evt = GetType().GetField("OnDayGenerated", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (evt != null)
            {
                var del = evt.GetValue(this) as System.Action<int, DailyWeather>;
                if (del != null && days != null && days.Count > 0)
                    del.Invoke(idx, days[idx]);
            }
        } catch { /* ok */ }

        // Optional: if you keep a "current" cache, update it here
        // current = (days != null && days.Count > 0) ? days[idx] : default;
    }
    
}