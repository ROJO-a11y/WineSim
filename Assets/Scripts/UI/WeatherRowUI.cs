using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;
using System.Reflection;

[DefaultExecutionOrder(400)]
public class WeatherRowUI : MonoBehaviour
{
    [Header("Text Fields (assign in Builder)")]
    public TMP_Text dateText;     // e.g., "Day 37 (2026)"
    public TMP_Text seasonText;   // e.g., "Autumn"
    public TMP_Text tempText;     // e.g., "12–18°C" or "16°C"
    public TMP_Text rainText;     // e.g., "Precip 3.2 mm"
    public TMP_Text windText;     // e.g., "Wind 12 kph"
    public TMP_Text humText;      // e.g., "Hum 65%"

    [Header("Optional")]
    public Image background;      // for theme tint, optional

    private void OnEnable()
    {
        TryWireEvents(true);
        RefreshFromSystems();
    }

    private void OnDisable()
    {
        TryWireEvents(false);
    }

    private void TryWireEvents(bool subscribe)
    {
        var t = TimeController.I;
        if (t == null) return;
        if (subscribe) t.OnNewDay += OnNewDay;
        else t.OnNewDay -= OnNewDay;
    }

    private void OnNewDay()
    {
        RefreshFromSystems();
    }

    public void RefreshFromSystems()
    {
        // Date/season from TimeController (and GameConfig start year if present)
        int dayIndex = SafeGetInt(TimeController.I, "Day", 0);
        int yearIdx  = SafeGetInt(TimeController.I, "Year", 0);
        string season= SafeGetString(TimeController.I, "SeasonName", "");

        int startYear = GetStartYearFromConfig();
        int calendarYear = (startYear > 0 ? startYear : 0) + yearIdx;
        if (calendarYear <= 0) calendarYear = yearIdx;

        if (dateText)   dateText.text   = $"Day {dayIndex}{(calendarYear>0 ? $" ({calendarYear})" : "")}";
        if (seasonText) seasonText.text = string.IsNullOrEmpty(season) ? "Season" : season;

        // Weather from WeatherSystem
        object wsys = FindFirstObjectByTypeSafe("WeatherSystem");
        object today = GetTodayWeather(wsys, dayIndex);

        // Extract values with soft reflection (works across naming variants)
        // Temperature
        float tMin = GetFloat(today, "tMinC", GetFloat(today, "minTempC", GetFloat(today, "minC", GetFloat(today, "tMin", 0f))));
        float tMax = GetFloat(today, "tMaxC", GetFloat(today, "maxTempC", GetFloat(today, "maxC", GetFloat(today, "tMax", 0f))));
        float tAvg = GetFloat(today, "tempC", GetFloat(today, "avgTempC", GetFloat(today, "temp", 0f)));

        // Precipitation
        float precipMm = GetFloat(today, "precipMm", GetFloat(today, "rainMm", GetFloat(today, "precipitation", 0f)));
        float precipPct= GetFloat(today, "precipPct", GetFloat(today, "precipitationPct", 0f));

        // Wind & humidity
        float windKph  = GetFloat(today, "windKph", GetFloat(today, "wind", 0f));
        float humidity = GetFloat(today, "humidity", 0f);

        if (tempText)
        {
            if (tMin != 0f || tMax != 0f) tempText.text = $"{Mathf.RoundToInt(tMin)}–{Mathf.RoundToInt(tMax)}°C";
            else if (tAvg != 0f)          tempText.text = $"{Mathf.RoundToInt(tAvg)}°C";
            else                          tempText.text = "–";
        }
        if (rainText)
        {
            if (precipMm > 0.01f)      rainText.text = $"Precip {precipMm:0.#} mm";
            else if (precipPct > 0.01f)rainText.text = $"Precip {precipPct:0}%";
            else                       rainText.text = "Precip –";
        }
        if (windText) windText.text = windKph > 0.01f ? $"Wind {windKph:0.#} kph" : "Wind –";
        if (humText)  humText.text  = humidity > 0.01f ? $"Hum {humidity:0.#}%" : "Hum –";
    }

    // --- Helpers ---

    private int GetStartYearFromConfig()
    {
        // Look for a holder in scene with a GameConfig and a start year (supports different naming)
        var holder = FindFirstObjectByTypeSafe("GameConfigHolder");
        object cfg = GetFirstOfType(holder, "GameConfig");
        if (cfg == null)
        {
            // Last resort: any loaded ScriptableObject named GameConfig
            var anyCfg = Resources.FindObjectsOfTypeAll<ScriptableObject>()
                                  .FirstOrDefault(o => o && o.GetType().Name == "GameConfig");
            cfg = anyCfg;
        }
        return ExtractStartYear(cfg);
    }

    private int ExtractStartYear(object cfg)
    {
        if (cfg == null) return 0;
        var t = cfg.GetType();

        // fields
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (f.FieldType != typeof(int)) continue;
            var n = f.Name.ToLowerInvariant();
            if ((n.Contains("start") && n.contains("year")) || n=="startyear" || n=="startingyear" || n=="baseyear")
                return (int)f.GetValue(cfg);
        }
        // properties
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.PropertyType != typeof(int)) continue;
            var n = p.Name.ToLowerInvariant();
            if ((n.Contains("start") && n.Contains("year")) || n=="startyear" || n=="startingyear" || n=="baseyear")
            {
                try { return (int)(p.GetValue(cfg) ?? 0); } catch {}
            }
        }
        return 0;
    }

    private object GetTodayWeather(object weatherSystem, int dayIndex)
    {
        if (weatherSystem == null) return null;
        var t = weatherSystem.GetType();

        // common methods
        var m = t.GetMethod("GetWeatherForDay", BindingFlags.Public | BindingFlags.Instance);
        if (m != null) return m.Invoke(weatherSystem, new object[]{ dayIndex });

        m = t.GetMethod("GetDayWeather", BindingFlags.Public | BindingFlags.Instance);
        if (m != null) return m.Invoke(weatherSystem, new object[]{ dayIndex });

        m = t.GetMethod("GetDay", BindingFlags.Public | BindingFlags.Instance);
        if (m != null) return m.Invoke(weatherSystem, new object[]{ dayIndex });

        // common properties
        var p = t.GetProperty("Today", BindingFlags.Public | BindingFlags.Instance);
        if (p != null) return p.GetValue(weatherSystem);

        p = t.GetProperty("Current", BindingFlags.Public | BindingFlags.Instance);
        if (p != null) return p.GetValue(weatherSystem);

        // arrays/lists
        var dailyField = t.GetField("daily", BindingFlags.Public | BindingFlags.Instance);
        if (dailyField != null)
        {
            var arr = dailyField.GetValue(weatherSystem) as System.Collections.IList;
            if (arr != null && dayIndex >= 0 && dayIndex < arr.Count) return arr[dayIndex];
        }
        return null;
    }

    private static object FindFirstObjectByTypeSafe(string typeName)
    {
        // Avoid hard dependency on concrete types—use name lookup
        var all = GameObject.FindObjectsOfType<MonoBehaviour>(true);
        return all.FirstOrDefault(mb => mb && mb.GetType().Name == typeName);
    }

    private object GetFirstOfType(object src, string wantedTypeName)
    {
        if (src == null) return null;
        var t = src.GetType();

        // fields
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = f.GetValue(src);
            if (val != null && val.GetType().Name == wantedTypeName) return val;
        }
        // properties
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            try
            {
                var val = p.GetValue(src);
                if (val != null && val.GetType().Name == wantedTypeName) return val;
            }
            catch {}
        }
        return null;
    }

    private int SafeGetInt(object src, string name, int fallback)
    {
        if (src == null) return fallback;

        var fi = src.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (fi != null && fi.FieldType == typeof(int)) return (int)fi.GetValue(src);

        var pi = src.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (pi != null && pi.PropertyType == typeof(int)) return (int)(pi.GetValue(src) ?? fallback);

        return fallback;
    }
    private string SafeGetString(object src, string name, string fallback)
    {
        if (src == null) return fallback;

        var fi = src.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (fi != null && fi.FieldType == typeof(string)) return (string)fi.GetValue(src);

        var pi = src.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (pi != null && pi.PropertyType == typeof(string)) return (string)(pi.GetValue(src) ?? fallback);

        return fallback;
    }

    private float GetFloat(object src, string name, float fallback)
    {
        if (src == null) return fallback;

        var fi = src.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (fi != null)
        {
            try { return Convert.ToSingle(fi.GetValue(src)); } catch {}
        }

        var pi = src.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (pi != null)
        {
            try { return Convert.ToSingle(pi.GetValue(src)); } catch {}
        }

        return fallback;
    }
}