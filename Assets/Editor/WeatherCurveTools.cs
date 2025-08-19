#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;

public static class WeatherCurveTools
{
    [MenuItem("Tools/WineSim/Curves/Set Bordeaux Preset (Curves)")]
    public static void SetBordeauxPreset()
    {
        var cfg = Selection.activeObject as ScriptableObject;
        if (cfg == null || cfg.GetType().Name != "WeatherConfig")
        {
            EditorUtility.DisplayDialog("Weather Curves", "Select your WeatherConfig asset first.", "OK");
            return;
        }

        // helper: set AnimationCurve field by name
        void SetCurve(string field, (float t, float v)[] kv)
        {
            var f = cfg.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(AnimationCurve)) return;
            var c = new AnimationCurve();
            foreach (var (t, v) in kv) c.AddKey(new Keyframe(t, v));
            // smooth
            for (int i = 0; i < c.length; i++) AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Auto);
            for (int i = 0; i < c.length; i++) AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Auto);
            f.SetValue(cfg, c);
        }

        // Curves
        SetCurve("tempYear", new (float,float)[] {
            (0.00f, 8f), (0.25f, 20f), (0.50f, 23f), (0.75f, 14f), (1.00f, 8f)
        });
        SetCurve("rainYear", new (float,float)[] {
            (0.00f, 3.7f), (0.25f, 2.2f), (0.50f, 1.7f), (0.75f, 3.1f), (1.00f, 3.7f)
        });
        SetCurve("humYear", new (float,float)[] {
            (0.00f, 85f), (0.25f, 76f), (0.50f, 68f), (0.75f, 78f), (1.00f, 86f)
        });
        SetCurve("solarYear", new (float,float)[] {
            (0.00f, 7f), (0.25f, 14f), (0.50f, 20f), (0.75f, 12f), (1.00f, 7f)
        });
        SetCurve("windYear", new (float,float)[] {
            (0.00f, 12f), (0.25f, 12f), (0.50f, 10f), (0.75f, 12f), (1.00f, 12f)
        });

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log("WeatherCurveTools: Set Bordeaux preset on selected WeatherConfig.");
    }

    [MenuItem("Tools/WineSim/Curves/Fit Curves From Monthly Arrays (if present)")]
    public static void FitCurvesFromMonthly()
    {
        var cfg = Selection.activeObject as ScriptableObject;
        if (cfg == null || cfg.GetType().Name != "WeatherConfig")
        {
            EditorUtility.DisplayDialog("Weather Curves", "Select your WeatherConfig asset first.", "OK");
            return;
        }

        float[] GetArr(params string[] names)
        {
            var t = cfg.GetType();
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(float[]))
                {
                    var arr = (float[])f.GetValue(cfg);
                    if (arr != null && arr.Length == 12) return arr;
                }
                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(float[]))
                {
                    var arr = (float[])p.GetValue(cfg, null);
                    if (arr != null && arr.Length == 12) return arr;
                }
            }
            return null;
        }

        void SetMonthlyCurve(string field, float[] arr)
        {
            if (arr == null) return;
            var t = cfg.GetType();
            var f = t.GetField(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(AnimationCurve)) return;

            var c = new AnimationCurve();
            // Mid-month anchors: (m + 0.5)/12
            for (int m = 0; m < 12; m++)
            {
                float time = (m + 0.5f) / 12f;
                c.AddKey(new Keyframe(time, arr[m]));
            }
            // loop-closing keys at 0 and 1 based on Jan/Dec
            c.AddKey(new Keyframe(0f, arr[0]));
            c.AddKey(new Keyframe(1f, arr[11]));

            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Auto);
            }
            f.SetValue(cfg, c);
        }

        // Pull arrays if you added them to WeatherConfig
        var tMin  = GetArr("tMinC12","minTempC12","tMin12");
        var tMax  = GetArr("tMaxC12","maxTempC12","tMax12");
        var rain  = GetArr("rainMmPerDay12","rain12");
        var hum   = GetArr("humidityPct12","humPct12","hum12");
        var sunH  = GetArr("sunHours12","sun12");
        var et0   = GetArr("et0MmPerDay12","et012");
        var wind  = GetArr("windKph12","wind12");

        // If both min/max exist, set tempYear to their average
        if (tMin != null && tMax != null)
        {
            var avg = new float[12];
            for (int i = 0; i < 12; i++) avg[i] = 0.5f * (tMin[i] + tMax[i]);
            SetMonthlyCurve("tempYear", avg);
        }

        SetMonthlyCurve("rainYear",  rain);
        SetMonthlyCurve("humYear",   hum);
        // For solarYear: if you only have sun hours, convert roughly to MJ/m² (very rough scale factor)
        if (sunH != null)
        {
            var mj = new float[12];
            for (int i = 0; i < 12; i++) mj[i] = Mathf.Lerp(5f, 22f, Mathf.Clamp01((sunH[i] - 2f) / 12f));
            SetMonthlyCurve("solarYear", mj);
        }
        SetMonthlyCurve("windYear",  wind);

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log("WeatherCurveTools: Fit curves from monthly arrays (if present) on selected WeatherConfig.");
    }
}
#endif