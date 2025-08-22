using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Stats screen UI
/// - Top: Cash, Market, Brand, Day
/// - Cash bars (current year by month, proper baseline; last-year line overlay if available)
/// - Bottles bars (current year by month, last-year line overlay if available)
/// - Weather monthly charts (Temp/Rain/Sun) with optional last-year overlay
/// Self-wires by child names so you don't have to drag refs.
/// </summary>
[DefaultExecutionOrder(500)]
public class StatsPanel : MonoBehaviour
{
    [Header("Top")]
    [SerializeField] TMP_Text cashText;    // TopRow/CashText
    [SerializeField] TMP_Text marketText;  // TopRow/MarketText
    [SerializeField] TMP_Text brandText;   // TopRow/BrandText
    [SerializeField] TMP_Text dayText;     // TopRow/DayText

    [Header("Cash Chart")]
    [SerializeField] RectTransform revenueChartArea;   // Revenue/ChartArea
    [SerializeField] TMP_Text revenueTitle;            // Revenue/Title
    [SerializeField] TMP_Text revenueAxisHint;         // Revenue/AxisHint
    [SerializeField] Color revenueBarColor = new Color(0.36f, 0.75f, 0.48f, 1f);
    [Tooltip("If ON, interpret daily cash series as net flow and SUM per month. If OFF, treat as balance and AVERAGE per month. Auto-detect also kicks in when negatives are present.")]
    [SerializeField] bool cashTreatAsNetFlow = false;

    [Header("Bottles Chart")]
    [SerializeField] RectTransform bottlesChartArea;   // Bottles/ChartArea
    [SerializeField] TMP_Text bottlesTitle;            // Bottles/Title
    [SerializeField] TMP_Text bottlesAxisHint;         // Bottles/AxisHint
    [SerializeField] Color bottlesBarColor = new Color(0.32f, 0.62f, 0.86f, 1f);

    [Header("Weather Charts (current year)")]
    [SerializeField] RectTransform weatherTempArea;   // Weather/Temp/ChartArea
    [SerializeField] TMP_Text      weatherTempTitle;  // Weather/Temp/Title
    [SerializeField] RectTransform weatherRainArea;   // Weather/Rain/ChartArea
    [SerializeField] TMP_Text      weatherRainTitle;  // Weather/Rain/Title
    [SerializeField] RectTransform weatherSunArea;    // Weather/Sun/ChartArea
    [SerializeField] TMP_Text      weatherSunTitle;   // Weather/Sun/Title
    [SerializeField] Color tempBarColor = new Color(0.95f, 0.45f, 0.35f, 1f);
    [SerializeField] Color rainBarColor = new Color(0.30f, 0.55f, 0.90f, 1f);
    [SerializeField] Color sunBarColor  = new Color(0.97f, 0.82f, 0.35f, 1f);

    [Header("Weather Compare")]
    [SerializeField] bool compareLastYear = true;
    [SerializeField] Color tempPrevColor = new Color(0.95f, 0.45f, 0.35f, 0.35f);
    [SerializeField] Color rainPrevColor = new Color(0.30f, 0.55f, 0.90f, 0.35f);
    [SerializeField] Color sunPrevColor  = new Color(0.97f, 0.82f, 0.35f, 0.35f);

    [Header("Weather Monthly Axis")]
    [SerializeField] float weatherChartMinHeight = 140f;
    [SerializeField] float weatherChartPreferredHeight = 160f;
    [SerializeField] float monthAxisHeight = 18f;
    [SerializeField] float monthLabelFontSize = 14f;
    [SerializeField] Color monthLabelColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] float overlayLineThickness = 3f;
    [SerializeField] float monthPadding = 4f;
    [SerializeField] bool monthlyBarsUseFixedWidth = true;
    [SerializeField] float monthlyBarWidth = 10f;
    [SerializeField] float monthlyBarMaxRatio = 0.5f;
    [SerializeField, Range(0.5f, 1f)] float monthlyWidthRatio = 0.8f;
    [SerializeField] float monthlyOuterPad = 8f;
    [SerializeField] bool  monthlyUseFixedSlotWidth = true;
    [SerializeField] float monthlySlotWidth = 22f;
    [SerializeField] float monthlySlotGap   = 6f;
    [SerializeField] bool monthlyFillFullWidth = true;
    [SerializeField] float monthlyEdgePad = 0f;
    [SerializeField] bool monthlyAlignLeft = true;
    [SerializeField] bool monthlyScaleToFit = true;
    [SerializeField, Range(0.25f, 10f)] float monthlyMaxScale = 4f;

    [Header("Generic bar styling")]
    [SerializeField] float barSpacing = 6f;
    [SerializeField] float minBarWidth = 8f;
    [SerializeField] float maxBarWidth = 40f;
    [SerializeField] float chartTopPadding = 8f;
    [SerializeField] float chartSidePadding = 8f;

    [Header("Stability")]
    [SerializeField] bool freezeCompletedMonthsFromRollingSources = true;
    private static System.Collections.Generic.Dictionary<int, float[]> _cashMonthCache = new System.Collections.Generic.Dictionary<int, float[]>();
    private static System.Collections.Generic.Dictionary<int, float[]> _botMonthCache = new System.Collections.Generic.Dictionary<int, float[]>();

    void Reset()      { AutoCache(); }
    void OnValidate() { if (!Application.isPlaying) AutoCache(); }

    void OnEnable()
    {
        AutoCache();
        StartCoroutine(BuildAllDeferred());
        if (TimeController.I) TimeController.I.OnNewDay += BuildAll;
    }
    void OnDisable()
    {
        if (TimeController.I) TimeController.I.OnNewDay -= BuildAll;
    }

    private IEnumerator BuildAllDeferred()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        BuildAll();
        Canvas.ForceUpdateCanvases();
    }

    private void BuildAll()
    {
        // --- Top row ---
        if (cashText)   cashText.text   = EconomySystem.I ? $"Cash: ${EconomySystem.I.Cash:n0}" : "Cash: ?";
        if (marketText) marketText.text = EconomySystem.I ? $"Market: {EconomySystem.I.MarketIndex:0.00}" : "Market: ?";
        if (brandText)  brandText.text  = EconomySystem.I ? $"Brand: {EconomySystem.I.BrandLevel:0.0}" : "Brand: ?";
        if (dayText)
        {
            if (TimeController.I)
            {
                int y = TimeController.I.Year + 1;
                int d = TimeController.I.DayOfYear + 1;
                dayText.text = $"Y{y} • Day {d}";
            }
            else dayText.text = "Day: ?";
        }

        // Make sure these sections stretch to the full width
        FixSectionAnchors(revenueChartArea);
        FixSectionAnchors(bottlesChartArea);

        BuildCashAndBottlesMonthly();
        BuildWeatherCharts();
    }

    // ---------- helpers ----------

    [SerializeField] bool debugLayout = false;

    private void ForceStretchX(RectTransform rt)
    {
        if (!rt) return;
        var a = rt.anchorMin; var b = rt.anchorMax;
        a.x = 0f; b.x = 1f;
        rt.anchorMin = a; rt.anchorMax = b;
        var offMin = rt.offsetMin; var offMax = rt.offsetMax;
        offMin.x = 0f; offMax.x = 0f;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    private void FixSectionAnchors(RectTransform chartArea)
    {
        if (!chartArea) return;
        var parent = chartArea.parent as RectTransform;
        ForceStretchX(parent);
        ForceStretchX(chartArea);
    }

    private void SanitizeArea(RectTransform area, float minHeight)
    {
        if (!area) return;

        var fit = area.GetComponent<ContentSizeFitter>();
        if (fit) { if (Application.isPlaying) Destroy(fit); else DestroyImmediate(fit); }

        var hlg = area.GetComponent<HorizontalLayoutGroup>();
        if (hlg) { if (Application.isPlaying) Destroy(hlg); else DestroyImmediate(hlg); }

        var bars = area.Find("Bars") as RectTransform;
        if (bars)
        {
            var fit2 = bars.GetComponent<ContentSizeFitter>();
            if (fit2) { if (Application.isPlaying) Destroy(fit2); else DestroyImmediate(fit2); }
            var hlg2 = bars.GetComponent<HorizontalLayoutGroup>();
            if (hlg2) { if (Application.isPlaying) Destroy(hlg2); else DestroyImmediate(hlg2); }
        }

        ForceStretchX(area);

        if (!area.GetComponent<RectMask2D>()) area.gameObject.AddComponent<RectMask2D>();

        var le = area.GetComponent<LayoutElement>() ?? area.gameObject.AddComponent<LayoutElement>();
        le.minHeight = Mathf.Max(le.minHeight, minHeight);
        if (le.preferredHeight < minHeight) le.preferredHeight = minHeight;
        le.flexibleHeight = 0f;

        le.minWidth = 0f;
        le.preferredWidth = -1f;
        le.flexibleWidth = 1f;

        if (debugLayout)
        {
            var r = area.rect;
            Debug.Log($"[StatsPanel] SanitizeArea '{area.name}' rect=({r.width:F0}x{r.height:F0})", area);
        }
    }

    private void EnsureChartAreaCapabilities(RectTransform area, float minHeight = 100f)
    {
        SanitizeArea(area, minHeight);
    }

    private int[] AggregateToBuckets(int[] src, int buckets)
    {
        if (src == null || src.Length == 0 || buckets <= 0) return new int[0];
        buckets = Mathf.Clamp(buckets, 1, Mathf.Max(1, src.Length));
        int[] outv = new int[buckets];
        float stride = (float)src.Length / buckets;
        for (int i = 0; i < buckets; i++)
        {
            int start = Mathf.FloorToInt(i * stride);
            int end = Mathf.FloorToInt((i + 1) * stride);
            if (end <= start) end = Mathf.Min(src.Length, start + 1);

            int sum = 0;
            for (int j = start; j < end && j < src.Length; j++) sum += src[j];
            outv[i] = sum;
        }
        return outv;
    }

    // ===== Weather =====

    private void BuildWeatherCharts()
    {
        EnsureWeatherUI();
        var ws = WeatherSystem.I;
        if (ws == null) return;

        int dpy = GameConfigHolder.Instance ? GameConfigHolder.Instance.Config.daysPerYear : 360;
        int yearIdx = TimeController.I ? TimeController.I.Year : 0;
        int curDayCount = TimeController.I ? (TimeController.I.DayOfYear + 1) : dpy; // 1..dpy

        // Resolve current year list (prefer archived)
        List<DailyWeather> curList = null;
        if (ws.days != null && ws.days.Count > 0) curList = ws.days;
        var histCur = ws.GetYear(yearIdx) as List<DailyWeather>;
        if (histCur != null && histCur.Count > 0) curList = histCur;
        if (curList == null || curList.Count == 0) return;

        AggregateWeatherToMonths(curList, dpy, out var temp12, out var rain12, out var sun12);

        float dpm = dpy / 12f;
        int completedMonths = Mathf.Clamp(Mathf.FloorToInt(curDayCount / dpm), 0, 12);

        float[] temp12Prev = null, rain12Prev = null, sun12Prev = null;
        if (compareLastYear && yearIdx > 0)
        {
            var prev = ws.GetYear(yearIdx - 1) as List<DailyWeather>;
            if (prev != null && prev.Count > 0)
            {
                AggregateWeatherToMonths(prev, dpy, out temp12Prev, out rain12Prev, out sun12Prev);
            }
        }

        if (weatherTempTitle) weatherTempTitle.text = "Temp (avg °C)";
        if (weatherRainTitle) weatherRainTitle.text = "Rain (avg mm/day)";
        if (weatherSunTitle)  weatherSunTitle.text  = "Sun (avg h/day)";

        BuildMonthlyBarsWithOverlay(weatherTempArea, temp12, completedMonths, temp12Prev, tempBarColor, tempPrevColor);
        BuildMonthlyBarsWithOverlay(weatherRainArea, rain12, completedMonths, rain12Prev, rainBarColor, rainPrevColor);
        BuildMonthlyBarsWithOverlay(weatherSunArea,  sun12,  completedMonths, sun12Prev,  sunBarColor,  sunPrevColor);

        if (weatherTempArea) LayoutRebuilder.ForceRebuildLayoutImmediate(weatherTempArea);
        if (weatherRainArea) LayoutRebuilder.ForceRebuildLayoutImmediate(weatherRainArea);
        if (weatherSunArea)  LayoutRebuilder.ForceRebuildLayoutImmediate(weatherSunArea);
        Canvas.ForceUpdateCanvases();
    }

    private void AggregateWeatherToMonths(List<DailyWeather> src, int dpy, out float[] temp12, out float[] rain12, out float[] sun12)
    {
        temp12 = new float[12];
        rain12 = new float[12];
        sun12  = new float[12];
        var cnt = new int[12];
        if (src == null || src.Count == 0) return;

        float dpm = dpy / 12f;
        int usable = Mathf.Min(dpy, src.Count);
        for (int i = 0; i < 12; i++)
        {
            int start = Mathf.FloorToInt(i * dpm);
            int end   = Mathf.FloorToInt((i + 1) * dpm);
            start = Mathf.Clamp(start, 0, usable);
            end   = Mathf.Clamp(end,   0, usable);
            if (end <= start) end = Mathf.Min(usable, start + 1);

            float t = 0f, r = 0f, s = 0f; int c = 0;
            for (int d = start; d < end; d++)
            {
                var dw = src[d];
                t += dw.tAvgC;
                r += dw.rainMm;
                s += dw.sunHours;
                c++;
            }
            cnt[i] = c;
            if (c > 0)
            {
                temp12[i] = t / c;
                rain12[i] = r / c; // average per day
                sun12[i]  = s / c;
            }
            else
            {
                temp12[i] = 0f; rain12[i] = 0f; sun12[i] = 0f;
            }
        }
    }

    // ===== Cash & Bottles (monthly) =====

    private void BuildCashAndBottlesMonthly()
    {
        int dpy = GameConfigHolder.Instance ? GameConfigHolder.Instance.Config.daysPerYear : 360;
        int curYear = TimeController.I ? TimeController.I.Year : 0;
        int prevYear = Mathf.Max(0, curYear - 1);
        int curDayCount = TimeController.I ? (TimeController.I.DayOfYear + 1) : dpy; // 1..dpy

        // Pull daily series from StatsTracker (via reflection helpers)
        int[] cashCur  = TryGetIntSeriesForYear(StatsTracker.I, "Cash",    curYear, dpy, dpy, curDayCount);
        int[] cashPrev = (curYear > 0) ? TryGetIntSeriesForYear(StatsTracker.I, "Cash",    prevYear, dpy, 0,   dpy) : null;

        int lastDays = Mathf.Min(180, dpy);
        int[] botCur  = TryGetIntSeriesForYear(StatsTracker.I, "Bottles", curYear, dpy, lastDays, curDayCount);
        int[] botPrev = (curYear > 0) ? TryGetIntSeriesForYear(StatsTracker.I, "Bottles", prevYear, dpy, 0, dpy) : null;

        // Month lengths
        int[] monthLensCur  = GetMonthLengths(dpy, curYear);
        int[] monthLensPrev = GetMonthLengths(dpy, prevYear);

        // Decide cash semantics: balance (avg) vs net flow (sum)
        bool hasNeg = HasNegative(cashCur) || HasNegative(cashPrev);
        bool treatAsFlow = cashTreatAsNetFlow || hasNeg;

        // Aggregate by calendar month
        float[] cashM12     = AggregateIntsToMonths(cashCur,  monthLensCur,  sum: treatAsFlow);
        float[] cashPrevM12 = (cashPrev != null && cashPrev.Length > 0) ? AggregateIntsToMonths(cashPrev, monthLensPrev, sum: treatAsFlow) : null;

        float[] botM12      = AggregateIntsToMonths(botCur,   monthLensCur,  sum: false); // avg/day per month
        float[] botPrevM12  = (botPrev != null && botPrev.Length > 0) ? AggregateIntsToMonths(botPrev, monthLensPrev, sum: false) : null;

        // Completed months
        int completedMonths = CountCompletedMonths(curDayCount, monthLensCur);

        // Stabilize (freeze completed months) for sources that may be rolling windows
        float[] cashStable = StabilizeWithCache(curYear, cashM12, completedMonths, _cashMonthCache);
        float[] botStable  = StabilizeWithCache(curYear, botM12,  completedMonths, _botMonthCache);

        // Titles
        if (revenueTitle)    revenueTitle.text    = treatAsFlow
            ? $"Cash flow (sum) by month (Y{curYear + 1})"
            : $"Cash (avg balance) by month (Y{curYear + 1})";
        if (revenueAxisHint) revenueAxisHint.text = "months";

        if (bottlesTitle)    bottlesTitle.text    = $"Bottles avg/day by month (Y{curYear + 1})";
        if (bottlesAxisHint) bottlesAxisHint.text = "months";

        // Draw charts
        // Cash uses a bipolar renderer so negative months show below the baseline
        var cashPrevLine = compareLastYear ? cashPrevM12 : null;
        if (cashPrevLine == null)
            Debug.Log("[StatsPanel] Cash: no previous-year series; overlay hidden.");

        BuildMonthlyBarsWithOverlayBipolar(
            revenueChartArea,
            cashStable,
            completedMonths,
            cashPrevLine,
            revenueBarColor,
            new Color(revenueBarColor.r, revenueBarColor.g, revenueBarColor.b, 0.35f)
        );

        // Bottles: regular (non-bipolar) monthly bars with optional overlay
        var botPrevLine = compareLastYear ? botPrevM12 : null;
        if (botPrevLine == null)
            Debug.Log("[StatsPanel] Bottles: no previous-year series; overlay hidden.");

        BuildMonthlyBarsWithOverlay(
            bottlesChartArea,
            botStable,
            completedMonths,
            botPrevLine,
            bottlesBarColor,
            new Color(bottlesBarColor.r, bottlesBarColor.g, bottlesBarColor.b, 0.35f)
        );

        if (revenueChartArea) LayoutRebuilder.ForceRebuildLayoutImmediate(revenueChartArea);
        if (bottlesChartArea) LayoutRebuilder.ForceRebuildLayoutImmediate(bottlesChartArea);
    }

    private bool HasNegative(int[] arr)
    {
        if (arr == null) return false;
        for (int i = 0; i < arr.Length; i++) if (arr[i] < 0) return true;
        return false;
    }

    // ===== Generic fixed-slot monthly bars (for non-negative data) =====

    private void BuildMonthlyBarsWithOverlay(RectTransform area, float[] current12, int completedMonths, float[] lastYear12, Color barColor, Color lineColor)
    {
        if (!area) return;
        SanitizeArea(area, weatherChartMinHeight);
        FixSectionAnchors(area);
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
        Canvas.ForceUpdateCanvases();

        var rect = area.rect;
        float totalW = rect.width;
        float totalH = rect.height;

        var bars = EnsureChild(area, "BarsFixed");
        var line = EnsureChild(area, "OverlayLine");
        var axis = EnsureChild(area, "Axis");
        var grid = EnsureChild(area, "Grid");
        grid.SetSiblingIndex(0);
        bars.SetSiblingIndex(1);
        line.SetSiblingIndex(2);
        axis.SetSiblingIndex(3);

        ClearChildren(grid); ClearChildren(bars); ClearChildren(line); ClearChildren(axis);
        StretchFull(bars); StretchFull(line); StretchFull(axis); StretchFull(grid);

        float axisH = Mathf.Max(12f, monthAxisHeight);
        float chartH = Mathf.Max(0f, totalH - chartTopPadding - axisH);
        int months = 12;

        // Full-width 12 slots
        float left = Mathf.Max(0f, monthlyEdgePad);
        float right = Mathf.Max(0f, monthlyEdgePad);
        float usableW = Mathf.Max(1f, totalW - left - right);
        float slotW = Mathf.Max(1f, usableW / months);
        float innerRight = left + usableW;

        float barW = monthlyBarsUseFixedWidth
            ? Mathf.Clamp(monthlyBarWidth, 1f, Mathf.Max(1f, slotW - 2f * monthPadding))
            : Mathf.Clamp(slotW * Mathf.Clamp01(monthlyBarMaxRatio), 1f, Mathf.Max(1f, slotW - 2f * monthPadding));

        System.Func<int, float> XCenter = (i) => Mathf.Clamp(left + slotW * (i + 0.5f), left + 0.5f, innerRight - 0.5f);

        // Baseline (bottom)
        var baseGo = new GameObject("Baseline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        baseGo.transform.SetParent(axis, false);
        var brt = baseGo.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0, 0);
        brt.pivot = new Vector2(0, 0);
        brt.sizeDelta = new Vector2(usableW, 2f);
        brt.anchoredPosition = new Vector2(left, axisH);
        baseGo.GetComponent<Image>().color = new Color(monthLabelColor.r, monthLabelColor.g, monthLabelColor.b, 0.25f);

        // Guides
        Color guide = new Color(monthLabelColor.r, monthLabelColor.g, monthLabelColor.b, 0.2f);
        for (int i = 0; i < months; i++)
        {
            float cx = XCenter(i);
            var g = new GameObject($"G_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            g.transform.SetParent(grid, false);
            var grt = g.GetComponent<RectTransform>();
            grt.anchorMin = grt.anchorMax = new Vector2(0, 0);
            grt.pivot = new Vector2(0.5f, 0f);
            grt.anchoredPosition = new Vector2(cx, axisH);
            grt.sizeDelta = new Vector2(1.5f, chartH);
            g.GetComponent<Image>().color = guide;
        }

        // Scale (only from current completed months + full prev)
        float max = 0f;
        if (current12 != null)
            for (int i = 0; i < Mathf.Min(months, completedMonths); i++) if (current12[i] > max) max = current12[i];
        if (lastYear12 != null)
            for (int i = 0; i < months; i++) if (lastYear12[i] > max) max = lastYear12[i];
        if (max <= 0f) max = 1f;

        // Bars
        for (int i = 0; i < months; i++)
        {
            if (i >= completedMonths || current12 == null) continue;
            float v = Mathf.Max(0f, current12[i]);
            float h = Mathf.Clamp(chartH * (v / max), 0f, chartH);
            float xCenter = XCenter(i);

            var go = new GameObject($"Bar_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(bars, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(barW, h);
            rt.anchoredPosition = new Vector2(xCenter, axisH);

            var img = go.GetComponent<Image>(); img.color = barColor;
        }

        // Overlay line
        if (lastYear12 != null)
        {
            Vector2? prev = null;
            float dotSize = Mathf.Max(4f, overlayLineThickness * 2f);
            for (int i = 0; i < months; i++)
            {
                float v = Mathf.Max(0f, lastYear12[i]);
                float xCenter = XCenter(i);
                float y = axisH + chartH * (v / max);
                var p = new Vector2(xCenter, y);

                var dot = new GameObject($"Dot_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dot.transform.SetParent(line, false);
                var drt = dot.GetComponent<RectTransform>();
                drt.anchorMin = drt.anchorMax = new Vector2(0, 0);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(dotSize, dotSize);
                drt.anchoredPosition = p;
                dot.GetComponent<Image>().color = lineColor;

                if (prev.HasValue)
                {
                    var q = prev.Value;
                    var seg = new GameObject($"Seg_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    seg.transform.SetParent(line, false);
                    var srt = seg.GetComponent<RectTransform>();
                    srt.anchorMin = srt.anchorMax = new Vector2(0, 0);
                    srt.pivot = new Vector2(0.5f, 0.5f);
                    float dist = Vector2.Distance(q, p);
                    srt.sizeDelta = new Vector2(dist, overlayLineThickness);
                    srt.anchoredPosition = (q + p) * 0.5f;
                    float ang = Mathf.Atan2(p.y - q.y, p.x - q.x) * Mathf.Rad2Deg;
                    srt.localRotation = Quaternion.Euler(0, 0, ang);
                    seg.GetComponent<Image>().color = lineColor;
                }
                prev = p;
            }
        }
        else
        {
            // Helpful log so you know why it's gone
            Debug.Log("[StatsPanel] BuildMonthlyBarsWithOverlay: lastYear12 missing; not drawing overlay.");
        }

        // Month labels
        string[] labs = new string[] { "J","F","M","A","M","J","J","A","S","O","N","D" };
        for (int i = 0; i < months; i++)
        {
            float xCenter = XCenter(i);
            var go = new GameObject($"M_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(axis, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(xCenter, 2f);
            var txt = go.GetComponent<TextMeshProUGUI>();
            txt.text = labs[i];
            txt.fontSize = monthLabelFontSize;
            txt.color = monthLabelColor;
            txt.alignment = TextAlignmentOptions.Midline;
            txt.enableWordWrapping = false;
            go.transform.SetAsLastSibling();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(bars);
        LayoutRebuilder.ForceRebuildLayoutImmediate(line);
        LayoutRebuilder.ForceRebuildLayoutImmediate(axis);
        LayoutRebuilder.ForceRebuildLayoutImmediate(grid);
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
        Canvas.ForceUpdateCanvases();
    }

    // ===== Cash-specific: bipolar (negative + positive) monthly bars with overlay =====

    private void BuildMonthlyBarsWithOverlayBipolar(RectTransform area, float[] current12, int completedMonths, float[] lastYear12, Color barColor, Color lineColor)
    {
        if (!area) return;
        SanitizeArea(area, weatherChartMinHeight);
        FixSectionAnchors(area);
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
        Canvas.ForceUpdateCanvases();

        var rect = area.rect;
        float totalW = rect.width;
        float totalH = rect.height;

        var bars = EnsureChild(area, "BarsFixed");
        var line = EnsureChild(area, "OverlayLine");
        var axis = EnsureChild(area, "Axis");
        var grid = EnsureChild(area, "Grid");
        grid.SetSiblingIndex(0);
        bars.SetSiblingIndex(1);
        line.SetSiblingIndex(2);
        axis.SetSiblingIndex(3);

        ClearChildren(grid); ClearChildren(bars); ClearChildren(line); ClearChildren(axis);
        StretchFull(bars); StretchFull(line); StretchFull(axis); StretchFull(grid);

        float axisH = Mathf.Max(12f, monthAxisHeight);
        float chartH = Mathf.Max(0f, totalH - chartTopPadding - axisH);
        int months = 12;

        float left = Mathf.Max(0f, monthlyEdgePad);
        float right = Mathf.Max(0f, monthlyEdgePad);
        float usableW = Mathf.Max(1f, totalW - left - right);
        float slotW = Mathf.Max(1f, usableW / months);
        float innerRight = left + usableW;

        float barW = monthlyBarsUseFixedWidth
            ? Mathf.Clamp(monthlyBarWidth, 1f, Mathf.Max(1f, slotW - 2f * monthPadding))
            : Mathf.Clamp(slotW * Mathf.Clamp01(monthlyBarMaxRatio), 1f, Mathf.Max(1f, slotW - 2f * monthPadding));

        System.Func<int, float> XCenter = (i) => Mathf.Clamp(left + slotW * (i + 0.5f), left + 0.5f, innerRight - 0.5f);

        // Determine min/max across both series (only full months from current)
        float minV = 0f, maxV = 0f;
        if (current12 != null)
        {
            for (int i = 0; i < Mathf.Min(months, completedMonths); i++)
            {
                minV = Mathf.Min(minV, current12[i]);
                maxV = Mathf.Max(maxV, current12[i]);
            }
        }
        if (lastYear12 != null)
        {
            for (int i = 0; i < months; i++)
            {
                minV = Mathf.Min(minV, lastYear12[i]);
                maxV = Mathf.Max(maxV, lastYear12[i]);
            }
        }
        if (Mathf.Approximately(minV, maxV))
        {
            // avoid flat line edge case
            maxV = Mathf.Max(maxV, 1f);
            minV = Mathf.Min(minV, 0f);
        }

        // Map a value to chart Y
        float MapY(float v)
        {
            float t = Mathf.InverseLerp(minV, maxV, v);
            return axisH + chartH * t;
        }

        float baselineY = MapY(0f);

        // Baseline
        var baseGo = new GameObject("BaselineZero", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        baseGo.transform.SetParent(axis, false);
        var brt = baseGo.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0, 0);
        brt.pivot = new Vector2(0, 0.5f);
        brt.sizeDelta = new Vector2(usableW, 2f);
        brt.anchoredPosition = new Vector2(left, baselineY - 1f);
        baseGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.35f);

        // Month guides
        Color guide = new Color(monthLabelColor.r, monthLabelColor.g, monthLabelColor.b, 0.2f);
        for (int i = 0; i < months; i++)
        {
            float cx = XCenter(i);
            var g = new GameObject($"G_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            g.transform.SetParent(grid, false);
            var grt = g.GetComponent<RectTransform>();
            grt.anchorMin = grt.anchorMax = new Vector2(0, 0);
            grt.pivot = new Vector2(0.5f, 0f);
            grt.anchoredPosition = new Vector2(cx, axisH);
            grt.sizeDelta = new Vector2(1.5f, chartH);
            g.GetComponent<Image>().color = guide;
        }

        // Bars (positive above baseline, negative below)
        if (current12 != null)
        {
            for (int i = 0; i < Mathf.Min(months, completedMonths); i++)
            {
                float v = current12[i];
                float xCenter = XCenter(i);
                float y0 = MapY(Mathf.Min(0f, v));
                float y1 = MapY(Mathf.Max(0f, v));
                float h = Mathf.Max(1f, Mathf.Abs(y1 - y0));

                var go = new GameObject($"Bar_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(bars, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(barW, h);
                rt.anchoredPosition = new Vector2(xCenter, Mathf.Min(y0, y1));
                var img = go.GetComponent<Image>(); img.color = barColor;
            }
        }

        // Overlay line (last year)
        if (lastYear12 != null)
        {
            Vector2? prev = null;
            float dotSize = Mathf.Max(4f, overlayLineThickness * 2f);
            for (int i = 0; i < months; i++)
            {
                float v = lastYear12[i];
                float xCenter = XCenter(i);
                float y = MapY(v);
                var p = new Vector2(xCenter, y);

                var dot = new GameObject($"Dot_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dot.transform.SetParent(line, false);
                var drt = dot.GetComponent<RectTransform>();
                drt.anchorMin = drt.anchorMax = new Vector2(0, 0);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(dotSize, dotSize);
                drt.anchoredPosition = p;
                dot.GetComponent<Image>().color = lineColor;

                if (prev.HasValue)
                {
                    var q = prev.Value;
                    var seg = new GameObject($"Seg_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    seg.transform.SetParent(line, false);
                    var srt = seg.GetComponent<RectTransform>();
                    srt.anchorMin = srt.anchorMax = new Vector2(0, 0);
                    srt.pivot = new Vector2(0.5f, 0.5f);
                    float dist = Vector2.Distance(q, p);
                    srt.sizeDelta = new Vector2(dist, overlayLineThickness);
                    srt.anchoredPosition = (q + p) * 0.5f;
                    float ang = Mathf.Atan2(p.y - q.y, p.x - q.x) * Mathf.Rad2Deg;
                    srt.localRotation = Quaternion.Euler(0, 0, ang);
                    seg.GetComponent<Image>().color = lineColor;
                }
                prev = p;
            }
        }
        else
        {
            Debug.Log("[StatsPanel] BuildMonthlyBarsWithOverlayBipolar: lastYear12 missing; not drawing overlay.");
        }

        // Month labels
        string[] labs = new string[] { "J","F","M","A","M","J","J","A","S","O","N","D" };
        for (int i = 0; i < months; i++)
        {
            float xCenter = XCenter(i);
            var go = new GameObject($"M_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(axis, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(xCenter, 2f);
            var txt = go.GetComponent<TextMeshProUGUI>();
            txt.text = labs[i];
            txt.fontSize = monthLabelFontSize;
            txt.color = monthLabelColor;
            txt.alignment = TextAlignmentOptions.Midline;
            txt.enableWordWrapping = false;
            go.transform.SetAsLastSibling();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(bars);
        LayoutRebuilder.ForceRebuildLayoutImmediate(line);
        LayoutRebuilder.ForceRebuildLayoutImmediate(axis);
        LayoutRebuilder.ForceRebuildLayoutImmediate(grid);
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
        Canvas.ForceUpdateCanvases();
    }

    // ===== Shared helpers for chart building =====

    private RectTransform EnsureChild(RectTransform parent, string name)
    {
        var rt = parent.Find(name) as RectTransform;
        if (!rt)
        {
            var go = new GameObject(name, typeof(RectTransform));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
        }
        return rt;
    }

    private void StretchFull(RectTransform rt)
    {
        if (!rt) return;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void ClearChildren(Transform t)
    {
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    // ===== Bottles float bars (kept for legacy helpers) =====

    private float[] AggregateToBucketsFloat(float[] src, int buckets, bool sum)
    {
        if (src == null || src.Length == 0 || buckets <= 0) return new float[0];
        buckets = Mathf.Clamp(buckets, 1, Mathf.Max(1, src.Length));
        float[] outv = new float[buckets];
        float stride = (float)src.Length / buckets;
        for (int i = 0; i < buckets; i++)
        {
            int start = Mathf.FloorToInt(i * stride);
            int end = Mathf.FloorToInt((i + 1) * stride);
            if (end <= start) end = Mathf.Min(src.Length, start + 1);

            float acc = 0f; int cnt = 0;
            for (int j = start; j < end && j < src.Length; j++) { acc += src[j]; cnt++; }
            outv[i] = sum ? acc : (cnt > 0 ? acc / cnt : 0f);
        }
        return outv;
    }

    private void BuildFloatBars(RectTransform area, float[] values, Color color)
    {
        if (!area) return;
        SanitizeArea(area, weatherChartMinHeight);

        var bars = area.Find("Bars") as RectTransform;
        if (!bars)
        {
            var go = new GameObject("Bars", typeof(RectTransform));
            bars = go.GetComponent<RectTransform>();
            bars.SetParent(area, false);
        }

        for (int i = bars.childCount - 1; i >= 0; i--) Destroy(bars.GetChild(i).gameObject);

        bars.anchorMin = new Vector2(0, 0);
        bars.anchorMax = new Vector2(1, 1);
        bars.pivot     = new Vector2(0, 0);
        bars.offsetMin = new Vector2(barSpacing, 0);
        bars.offsetMax = new Vector2(-barSpacing, 0);

        var hlg = bars.GetComponent<HorizontalLayoutGroup>() ?? bars.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = barSpacing;
        hlg.childControlWidth = true;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.LowerLeft;

        if (values == null || values.Length == 0) { LayoutRebuilder.ForceRebuildLayoutImmediate(area); return; }

        float max = 0f;
        for (int i = 0; i < values.Length; i++) if (values[i] > max) max = values[i];
        if (max <= 0f) max = 1f;

        var rect = area.rect;
        float availableW = Mathf.Max(0f, rect.width - (values.Length + 1) * barSpacing);
        float w = values.Length > 0 ? availableW / values.Length : 0f;
        w = Mathf.Clamp(w, minBarWidth, maxBarWidth);

        float chartH = Mathf.Max(0f, rect.height - chartTopPadding);

        for (int i = 0; i < values.Length; i++)
        {
            float h = chartH * (values[i] / max);

            var go = new GameObject($"WBar_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(bars, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(w, h);

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = w;
            le.flexibleWidth = 0f;
            le.preferredHeight = h;
            le.flexibleHeight = 0f;

            var img = go.GetComponent<Image>();
            img.color = color;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(bars);
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
    }

    private void BuildFloatBarsOverlay(RectTransform area, float[] cur, float[] prev, Color curColor, Color prevColor)
    {
        if (!area) return;
        SanitizeArea(area, weatherChartMinHeight);

        int len = (cur != null) ? cur.Length : 0;
        if (len == 0) { BuildFloatBars(area, cur, curColor); return; }
        if (prev == null || prev.Length != len)
        {
            BuildFloatBars(area, cur, curColor);
            return;
        }

        var prevRt = area.Find("BarsPrev") as RectTransform;
        if (!prevRt)
        {
            var go = new GameObject("BarsPrev", typeof(RectTransform));
            prevRt = go.GetComponent<RectTransform>();
            prevRt.SetParent(area, false);
            prevRt.SetSiblingIndex(0);
        }
        var curRt = area.Find("Bars") as RectTransform;
        if (!curRt)
        {
            var go = new GameObject("Bars", typeof(RectTransform));
            curRt = go.GetComponent<RectTransform>();
            curRt.SetParent(area, false);
            curRt.SetSiblingIndex(1);
        }

        void SetupContainer(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0, 0);
            rt.offsetMin = new Vector2(barSpacing, 0);
            rt.offsetMax = new Vector2(-barSpacing, 0);

            var hlg = rt.GetComponent<HorizontalLayoutGroup>() ?? rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = barSpacing;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.LowerLeft;
        }
        SetupContainer(prevRt);
        SetupContainer(curRt);

        for (int i = prevRt.childCount - 1; i >= 0; i--) Destroy(prevRt.GetChild(i).gameObject);
        for (int i = curRt.childCount - 1; i >= 0; i--) Destroy(curRt.GetChild(i).gameObject);

        var rect = area.rect;
        float availableW = Mathf.Max(0f, rect.width - (len + 1) * barSpacing);
        float w = len > 0 ? availableW / len : 0f;
        w = Mathf.Clamp(w, minBarWidth, maxBarWidth);
        float chartH = Mathf.Max(0f, rect.height - chartTopPadding);

        float max = 0f;
        for (int i = 0; i < len; i++)
        {
            if (cur[i] > max) max = cur[i];
            if (prev[i] > max) max = prev[i];
        }
        if (max <= 0f) max = 1f;

        for (int i = 0; i < len; i++)
        {
            float h = chartH * (prev[i] / max);
            var go = new GameObject($"PBar_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(prevRt, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = w; le.flexibleWidth = 0f; le.preferredHeight = h; le.flexibleHeight = 0f;
            var img = go.GetComponent<Image>();
            img.color = prevColor;
        }

        for (int i = 0; i < len; i++)
        {
            float h = chartH * (cur[i] / max);
            var go = new GameObject($"Bar_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(curRt, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = w; le.flexibleWidth = 0f; le.preferredHeight = h; le.flexibleHeight = 0f;
            var img = go.GetComponent<Image>();
            img.color = curColor;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(prevRt);
        LayoutRebuilder.ForceRebuildLayoutImmediate(curRt);
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
    }

    private void EnsureWeatherUI()
    {
        Transform W(string path) => transform.Find(path);

        var weather = W("Weather");
        RectTransform weatherRT = null;
        if (!weather)
        {
            var go = new GameObject("Weather", typeof(RectTransform), typeof(VerticalLayoutGroup));
            weather = go.transform;
            var rt = (RectTransform)weather;
            rt.SetParent(transform, false);
            weatherRT = rt;

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 8f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }
        else
        {
            weatherRT = weather as RectTransform;
        }

        ForceStretchX(weatherRT);

        void EnsureSection(string name, ref RectTransform area, ref TMP_Text title)
        {
            RectTransform secRT = null;
            var sec = weather.Find(name);
            if (!sec)
            {
                var sgo = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
                sec = sgo.transform;
                secRT = (RectTransform)sec;
                secRT.SetParent(weather, false);
                ForceStretchX(secRT);

                var vlg = sgo.GetComponent<VerticalLayoutGroup>();
                vlg.spacing = 4f;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(sec, false);
                var t = titleGo.GetComponent<TextMeshProUGUI>();
                t.enableWordWrapping = false; t.fontSize = 20f; t.text = name;

                var chartGo = new GameObject("ChartArea", typeof(RectTransform));
                chartGo.transform.SetParent(sec, false);
                var crt = chartGo.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0f, 0f);
                crt.anchorMax = new Vector2(1f, 0f);
                crt.pivot     = new Vector2(0.5f, 0f);
                crt.sizeDelta = new Vector2(0f, weatherChartPreferredHeight);

                SanitizeArea(crt, weatherChartMinHeight);

                title = t; area = crt;
            }
            else
            {
                secRT = (RectTransform)sec;
                ForceStretchX(secRT);

                if (!title)
                {
                    var t = sec.Find("Title")?.GetComponent<TextMeshProUGUI>();
                    if (t) title = t;
                }
                if (!area)
                {
                    var crt = sec.Find("ChartArea") as RectTransform;
                    if (crt) { area = crt; ForceStretchX(area); }
                }
            }
        }

        EnsureSection("Temp", ref weatherTempArea, ref weatherTempTitle);
        EnsureSection("Rain", ref weatherRainArea, ref weatherRainTitle);
        EnsureSection("Sun",  ref weatherSunArea,  ref weatherSunTitle);
    }

    // --- auto-wire minimal UI ---
    private void AutoCache()
    {
        cashText   = cashText   ? cashText   : transform.Find("TopRow/CashText")  ?.GetComponent<TMP_Text>();
        marketText = marketText ? marketText : transform.Find("TopRow/MarketText")?.GetComponent<TMP_Text>();
        brandText  = brandText  ? brandText  : transform.Find("TopRow/BrandText") ?.GetComponent<TMP_Text>();
        dayText    = dayText    ? dayText    : transform.Find("TopRow/DayText")   ?.GetComponent<TMP_Text>();

        revenueChartArea = revenueChartArea ? revenueChartArea : transform.Find("Revenue/ChartArea") as RectTransform;
        revenueTitle     = revenueTitle     ? revenueTitle     : transform.Find("Revenue/Title")     ?.GetComponent<TMP_Text>();
        revenueAxisHint  = revenueAxisHint  ? revenueAxisHint  : transform.Find("Revenue/AxisHint")  ?.GetComponent<TMP_Text>();

        bottlesChartArea = bottlesChartArea ? bottlesChartArea : transform.Find("Bottles/ChartArea") as RectTransform;
        bottlesTitle     = bottlesTitle     ? bottlesTitle     : transform.Find("Bottles/Title")     ?.GetComponent<TMP_Text>();
        bottlesAxisHint  = bottlesAxisHint  ? bottlesAxisHint  : transform.Find("Bottles/AxisHint")  ?.GetComponent<TMP_Text>();

        weatherTempArea = weatherTempArea ? weatherTempArea : transform.Find("Weather/Temp/ChartArea") as RectTransform;
        weatherTempTitle = weatherTempTitle ? weatherTempTitle : transform.Find("Weather/Temp/Title")?.GetComponent<TMP_Text>();
        weatherRainArea = weatherRainArea ? weatherRainArea : transform.Find("Weather/Rain/ChartArea") as RectTransform;
        weatherRainTitle = weatherRainTitle ? weatherRainTitle : transform.Find("Weather/Rain/Title")?.GetComponent<TMP_Text>();
        weatherSunArea = weatherSunArea ? weatherSunArea : transform.Find("Weather/Sun/ChartArea") as RectTransform;
        weatherSunTitle = weatherSunTitle ? weatherSunTitle : transform.Find("Weather/Sun/Title")?.GetComponent<TMP_Text>();
    }

    // ===== Month/Year helpers =====

    private int[] NormalizeYearArray(int[] arr, int dpy)
    {
        if (arr == null) return new int[0];
        if (arr.Length == dpy) return arr;
        var outv = new int[dpy];
        int n = Mathf.Min(dpy, arr.Length);
        System.Array.Copy(arr, 0, outv, 0, n);
        return outv;
    }

    private int[] GetMonthLengths(int dpy, int yearIndex)
    {
        if (dpy >= 365)
        {
            var months = new int[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            if (dpy == 366) months[1] = 29;
            return months;
        }

        var arr = new int[12];
        int baseLen = Mathf.Max(1, dpy / 12);
        int rem = Mathf.Max(0, dpy - baseLen * 12);
        for (int i = 0; i < 12; i++) arr[i] = baseLen;
        for (int i = 0; i < rem; i++) arr[i]++;
        return arr;
    }

    private int CountCompletedMonths(int daysElapsed, int[] monthLengths)
    {
        int completed = 0;
        int acc = 0;
        for (int i = 0; i < 12; i++)
        {
            acc += Mathf.Max(1, monthLengths[i]);
            if (daysElapsed >= acc) completed++; else break;
        }
        return Mathf.Clamp(completed, 0, 12);
    }

    private float[] AggregateIntsToMonths(int[] src, int[] monthLengths, bool sum)
    {
        var outv = new float[12];
        if (src == null || src.Length == 0 || monthLengths == null || monthLengths.Length != 12) return outv;

        int idx = 0;
        for (int m = 0; m < 12; m++)
        {
            int len = Mathf.Max(1, monthLengths[m]);
            int end = Mathf.Min(src.Length, idx + len);
            int acc = 0; int cnt = 0;
            for (int d = idx; d < end; d++) { acc += src[d]; cnt++; }
            outv[m] = sum ? acc : (cnt > 0 ? (float)acc / cnt : 0f);
            idx = end;
        }
        return outv;
    }

    private int[] RightAlignToYear(int[] src, int dpy, int curDayCount)
    {
        if (src == null) return new int[0];
        var outv = new int[dpy];
        int n = Mathf.Min(src.Length, dpy, Mathf.Max(1, curDayCount));
        int dstStart = Mathf.Max(0, curDayCount - n);
        int srcStart = Mathf.Max(0, src.Length - n);
        for (int i = 0; i < n; i++) outv[dstStart + i] = src[srcStart + i];
        return outv;
    }

    private float[] StabilizeWithCache(int year, float[] aggregated12, int completedMonths, System.Collections.Generic.Dictionary<int, float[]> cache)
    {
        if (!freezeCompletedMonthsFromRollingSources)
            return aggregated12 ?? new float[12];

        if (aggregated12 == null || aggregated12.Length != 12)
            aggregated12 = new float[12];

        if (!cache.TryGetValue(year, out var frozen))
        {
            frozen = new float[12];
            cache[year] = frozen;
        }

        var outv = new float[12];
        for (int i = 0; i < 12; i++)
        {
            if (i < completedMonths)
            {
                if (Mathf.Approximately(frozen[i], 0f) && !Mathf.Approximately(aggregated12[i], 0f))
                    frozen[i] = aggregated12[i];
                outv[i] = frozen[i];
            }
            else
            {
                outv[i] = 0f;
            }
        }
        return outv;
    }

    // Reflection-based fetch from StatsTracker
    private int[] TryGetIntSeriesForYear(object statsTracker, string baseName, int yearIndex, int dpy, int fallbackLastDays, int curDayCount)
    {
        if (statsTracker == null) return new int[0];
        var t = statsTracker.GetType();

        var m = t.GetMethod($"Get{baseName}Year", BindingFlags.Public | BindingFlags.Instance);
        if (m != null)
        {
            var obj = m.Invoke(statsTracker, new object[] { yearIndex });
            if (obj is int[] arr && arr.Length > 0) return NormalizeYearArray(arr, dpy);
        }
        m = t.GetMethod($"Get{baseName}DaysForYear", BindingFlags.Public | BindingFlags.Instance);
        if (m != null)
        {
            var obj = m.Invoke(statsTracker, new object[] { yearIndex });
            if (obj is int[] arr && arr.Length > 0) return NormalizeYearArray(arr, dpy);
        }
        m = t.GetMethod($"Get{baseName}DailyForYear", BindingFlags.Public | BindingFlags.Instance);
        if (m != null)
        {
            var obj = m.Invoke(statsTracker, new object[] { yearIndex });
            if (obj is int[] arr && arr.Length > 0) return NormalizeYearArray(arr, dpy);
        }

        // Fallback (current year only): use a rolling window if available and right-align to today
        if (fallbackLastDays > 0 && yearIndex == (TimeController.I ? TimeController.I.Year : 0))
        {
            m = t.GetMethod($"Get{baseName}LastDays", BindingFlags.Public | BindingFlags.Instance);
            if (m != null)
            {
                var obj = m.Invoke(statsTracker, new object[] { fallbackLastDays });
                if (obj is int[] arr && arr.Length > 0)
                {
                    if (arr.Length >= dpy)
                    {
                        var outvFull = new int[dpy];
                        System.Array.Copy(arr, arr.Length - dpy, outvFull, 0, dpy);
                        return outvFull;
                    }
                    return RightAlignToYear(arr, dpy, Mathf.Clamp(curDayCount, 1, dpy));
                }
            }
        }

        return new int[0];
    }
}