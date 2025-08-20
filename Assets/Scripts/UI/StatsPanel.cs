using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Stats screen UI
/// - Top: Cash, Market, Brand, Day
/// - Revenue bars (last year, 12 buckets)
//  - Bottles bars (last 180 days, 24 buckets)
//  - Weather monthly charts (Temp/Rain/Sun) with optional last-year overlay
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

    [Header("Revenue Chart")]
    [SerializeField] RectTransform revenueChartArea;   // Revenue/ChartArea
    [SerializeField] TMP_Text revenueTitle;            // Revenue/Title
    [SerializeField] TMP_Text revenueAxisHint;         // Revenue/AxisHint
    [SerializeField] Color revenueBarColor = new Color(0.36f, 0.75f, 0.48f, 1f);

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
    [SerializeField] float weatherChartMinHeight = 140f;       // ensures enough headroom so bars don't look clipped
    [SerializeField] float weatherChartPreferredHeight = 160f;  // default height for runtime-built ChartAreas
    [SerializeField] float monthAxisHeight = 18f;
    [SerializeField] float monthLabelFontSize = 14f;
    [SerializeField] Color monthLabelColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] float overlayLineThickness = 3f;
    [SerializeField] float monthPadding = 4f; // inner left/right padding per month slot so 12 bars always fit
    [SerializeField] bool monthlyBarsUseFixedWidth = true;   // if true, use a fixed pixel width per bar
    [SerializeField] float monthlyBarWidth = 10f;            // fixed width (px) for monthly bars
    [SerializeField] float monthlyBarMaxRatio = 0.5f;        // if not fixed, use ratio of slot width (0..1)
    [SerializeField, Range(0.5f, 1f)] float monthlyWidthRatio = 0.8f; // portion of area width used by month slots
    [SerializeField] float monthlyOuterPad = 8f;                       // extra left/right padding inside the inner region
    [SerializeField] bool  monthlyUseFixedSlotWidth = true;   // Use a fixed slot + gap width so the whole 12-month grid is compact
    [SerializeField] float monthlySlotWidth = 22f;            // Visual width allocated to each month slot (px)
    [SerializeField] float monthlySlotGap   = 6f;             // Gap between month slots (px)
    [SerializeField] bool monthlyAlignLeft = true;            // align the 12-month grid to the left instead of centering
    [SerializeField] bool monthlyScaleToFit = true;           // allow the fixed-slot grid to expand to fill available width
    [SerializeField, Range(0.25f, 10f)] float monthlyMaxScale = 4f; // allow more expansion so grid can fill width

    [Header("Bar styling")]
    [SerializeField] float barSpacing = 6f;
    [SerializeField] float minBarWidth = 8f;
    [SerializeField] float maxBarWidth = 40f;
    [SerializeField] float chartTopPadding = 8f;

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
        // wait for one frame so RectTransforms get correct sizes
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

        // --- Revenue (last year) ---
        int dpy = GameConfigHolder.Instance ? GameConfigHolder.Instance.Config.daysPerYear : 360;
        var revDays = StatsTracker.I ? StatsTracker.I.GetRevenueLastDays(dpy) : System.Array.Empty<int>();
        var revAgg = AggregateToBuckets(revDays, 12);
        if (revenueTitle) revenueTitle.text = "Revenue (last year)";
        if (revenueAxisHint) revenueAxisHint.text = "months";
        BuildBars(revenueChartArea, revAgg, revenueBarColor);

        // --- Bottles (last 180d) ---
        var botDays = StatsTracker.I ? StatsTracker.I.GetBottlesLastDays(Mathf.Min(180, dpy)) : System.Array.Empty<int>();
        var botAgg = AggregateToBuckets(botDays, 24);
        if (bottlesTitle) bottlesTitle.text = "Inventory bottles (last 180d)";
        if (bottlesAxisHint) bottlesAxisHint.text = "weeks";
        BuildBars(bottlesChartArea, botAgg, bottlesBarColor);

        if (revenueChartArea) LayoutRebuilder.ForceRebuildLayoutImmediate(revenueChartArea);
        if (bottlesChartArea) LayoutRebuilder.ForceRebuildLayoutImmediate(bottlesChartArea);

        BuildWeatherCharts();
    }

    // ---------- helpers ----------

    [SerializeField] bool debugLayout = false; // optional logs

    // Force horizontal stretch (keep vertical settings intact)
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

        // Remove conflicting ContentSizeFitter from ChartArea and Bars (we manage sizing manually)
        var fit = area.GetComponent<ContentSizeFitter>();
        if (fit) { if (Application.isPlaying) Destroy(fit); else DestroyImmediate(fit); }
        var bars = area.Find("Bars") as RectTransform;
        if (bars)
        {
            var fit2 = bars.GetComponent<ContentSizeFitter>();
            if (fit2) { if (Application.isPlaying) Destroy(fit2); else DestroyImmediate(fit2); }
        }

        // Force horizontal stretch; height is managed via LayoutElement
        ForceStretchX(area);

        // Clip outside rendering
        if (!area.GetComponent<RectMask2D>()) area.gameObject.AddComponent<RectMask2D>();

        // Ensure a preferred height so layout doesn’t collapse and allow width to expand
        var le = area.GetComponent<LayoutElement>() ?? area.gameObject.AddComponent<LayoutElement>();
        le.minHeight = Mathf.Max(le.minHeight, minHeight);
        if (le.preferredHeight < minHeight) le.preferredHeight = minHeight;
        le.flexibleHeight = 0f;
        // Let the area take all available width from parent VLG
        le.minWidth = 0f;
        le.preferredWidth = -1f; // use layout width
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

    private void BuildBars(RectTransform area, int[] values, Color color)
    {
        if (!area) return;
        SanitizeArea(area, weatherChartMinHeight);

        // Bars container (stretch to area; no ContentSizeFitter here)
        var bars = area.Find("Bars") as RectTransform;
        if (!bars)
        {
            var go = new GameObject("Bars", typeof(RectTransform));
            bars = go.GetComponent<RectTransform>();
            bars.SetParent(area, false);
        }

        for (int i = bars.childCount - 1; i >= 0; i--) Destroy(bars.GetChild(i).gameObject);

        // Stretch the Bars container to the ChartArea
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

            var go = new GameObject($"Bar_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
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

        // Aggregate to 12 months
        AggregateWeatherToMonths(curList, dpy, out var temp12, out var rain12, out var sun12);

        // How many *full* months are complete?
        float dpm = dpy / 12f;
        int completedMonths = Mathf.Clamp(Mathf.FloorToInt(curDayCount / dpm), 0, 12);

        // Previous year overlay
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
                rain12[i] = r / c; // average per day as requested
                sun12[i]  = s / c;
            }
            else
            {
                temp12[i] = 0f; rain12[i] = 0f; sun12[i] = 0f;
            }
        }
    }

    private void BuildMonthlyBarsWithOverlay(RectTransform area, float[] current12, int completedMonths, float[] lastYear12, Color barColor, Color lineColor)
    {
        if (!area) return;
        SanitizeArea(area, weatherChartMinHeight);

        // Also force anchors on parent + area to ensure width is correct
        FixSectionAnchors(area);
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
        Canvas.ForceUpdateCanvases();

        // IMPORTANT: use local rect units
        var rect = area.rect;
        float totalW = rect.width;
        float totalH = rect.height;

        // Containers
        var bars = area.Find("BarsFixed") as RectTransform;
        if (!bars) { var go = new GameObject("BarsFixed", typeof(RectTransform)); bars = go.GetComponent<RectTransform>(); bars.SetParent(area, false); }
        var line = area.Find("OverlayLine") as RectTransform;
        if (!line) { var go = new GameObject("OverlayLine", typeof(RectTransform)); line = go.GetComponent<RectTransform>(); line.SetParent(area, false); }
        var axis = area.Find("Axis") as RectTransform;
        if (!axis) { var go = new GameObject("Axis", typeof(RectTransform)); axis = go.GetComponent<RectTransform>(); axis.SetParent(area, false); }
        var grid = area.Find("Grid") as RectTransform;
        if (!grid) { var go = new GameObject("Grid", typeof(RectTransform)); grid = go.GetComponent<RectTransform>(); grid.SetParent(area, false); }

        // Layering: Grid (back) → Bars → OverlayLine → Axis (labels on top)
        grid.SetSiblingIndex(0);
        bars.SetSiblingIndex(1);
        line.SetSiblingIndex(2);
        axis.SetSiblingIndex(3);

        // Clear children
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
        for (int i = bars.childCount - 1; i >= 0; i--) Destroy(bars.GetChild(i).gameObject);
        for (int i = line.childCount - 1; i >= 0; i--) Destroy(line.GetChild(i).gameObject);
        for (int i = axis.childCount - 1; i >= 0; i--) Destroy(axis.GetChild(i).gameObject);

        // Stretch containers to area
        void Stretch(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0f, 0f);
            var offMin = rt.offsetMin; var offMax = rt.offsetMax;
            offMin.x = 0f; offMax.x = 0f; offMin.y = 0f; offMax.y = 0f;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
        }
        Stretch(bars); Stretch(line); Stretch(axis); Stretch(grid);

        float axisH = Mathf.Max(12f, monthAxisHeight);
        float chartH = Mathf.Max(0f, totalH - chartTopPadding - axisH);
        int months = 12;
        float left, right, usableW, slotW;

        // Shared providers filled by branches below
        System.Func<int, float> xCenterProvider = null;  // maps month index → x position (unclamped)
        float visualSlotWidthOverride = -1f;             // when >=0, use for bar width
        float usedWOverride = -1f;                       // actual used inner width when fixed branch scales

        if (monthlyUseFixedSlotWidth)
        {
            // Fixed visual slot width + gap, but optionally scale to fill width
            float slotWidthDraw = Mathf.Max(1f, monthlySlotWidth);
            float slotGapDraw   = Mathf.Max(0f, monthlySlotGap);
            float usedWRaw = months * slotWidthDraw + (months - 1) * slotGapDraw;

            float pad = Mathf.Max(0f, monthlyOuterPad);
            float available = Mathf.Max(1f, totalW - (pad + pad));
            float scaleTarget = available / usedWRaw;
            float scale = monthlyScaleToFit ? Mathf.Clamp(scaleTarget, 0.25f, monthlyMaxScale)
                                            : Mathf.Clamp01(scaleTarget);

            float slotWidthScaled = slotWidthDraw * scale;
            float slotGapScaled   = slotGapDraw * scale;
            float stepScaled      = slotWidthScaled + slotGapScaled;
            float usedWScaled     = (months - 1) * stepScaled + slotWidthScaled;

            if (monthlyAlignLeft)
            {
                left  = pad;
                right = Mathf.Max(0f, totalW - usedWScaled - left);
            }
            else
            {
                float side = Mathf.Max(0f, (totalW - usedWScaled) * 0.5f);
                left  = side;
                right = side;
            }

            usableW = Mathf.Max(1f, totalW - left - right);
            slotW   = stepScaled;

            float _slotWidthScaled = slotWidthScaled;
            float _slotGapScaled   = slotGapScaled;

            float XCenterFixed(int i) => left + (_slotWidthScaled * 0.5f) + i * (_slotWidthScaled + _slotGapScaled);
            xCenterProvider = XCenterFixed;
            visualSlotWidthOverride = _slotWidthScaled;
            usedWOverride = usedWScaled;
        }
        else
        {
            float innerW = totalW * Mathf.Clamp01(monthlyWidthRatio);
            float lr = Mathf.Max(0f, (totalW - innerW) * 0.5f);
            left = lr + Mathf.Max(0f, monthlyOuterPad);
            right = lr + Mathf.Max(0f, monthlyOuterPad);
            usableW = Mathf.Max(1f, totalW - left - right);
            slotW = Mathf.Max(1f, usableW / months);
            xCenterProvider = (i) => left + slotW * (i + 0.5f);
            usedWOverride = Mathf.Min(usableW, totalW - left - right);
        }

        float innerRight = left + (usedWOverride > 0f ? usedWOverride : Mathf.Min(usableW, totalW - left - right));
        float VisualSlot() => visualSlotWidthOverride >= 0f ? visualSlotWidthOverride : (monthlyUseFixedSlotWidth ? monthlySlotWidth : slotW);
        float XCenter(int i)
        {
            float xc = xCenterProvider != null ? xCenterProvider(i) : (left + slotW * (i + 0.5f));
            return Mathf.Clamp(xc, left + 0.5f, innerRight - 0.5f);
        }

        float visualSlotWidth = Mathf.Max(1f, VisualSlot());
        float barW = monthlyBarsUseFixedWidth
            ? Mathf.Clamp(monthlyBarWidth, 1f, Mathf.Max(1f, visualSlotWidth - 2f * monthPadding))
            : Mathf.Clamp(visualSlotWidth * Mathf.Clamp01(monthlyBarMaxRatio), 1f, Mathf.Max(1f, visualSlotWidth - 2f * monthPadding));

        // Axis baseline
        var baseGo = new GameObject("Baseline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        baseGo.transform.SetParent(axis, false);
        var brt = baseGo.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0, 0);
        brt.pivot = new Vector2(0, 0);
        float baselineW = Mathf.Max(0f, innerRight - left);
        brt.sizeDelta = new Vector2(baselineW, 2f);
        brt.anchoredPosition = new Vector2(left, axisH);
        baseGo.GetComponent<Image>().color = new Color(monthLabelColor.r, monthLabelColor.g, monthLabelColor.b, 0.25f);

        // Month guides
        Color guide = new Color(monthLabelColor.r, monthLabelColor.g, monthLabelColor.b, 0.2f);
        for (int i = 0; i < months; i++)
        {
            float xCenter = XCenter(i);
            var g = new GameObject($"G_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            g.transform.SetParent(grid, false);
            var grt = g.GetComponent<RectTransform>();
            grt.anchorMin = grt.anchorMax = new Vector2(0, 0);
            grt.pivot = new Vector2(0.5f, 0f);
            grt.sizeDelta = new Vector2(1.5f, chartH);
            grt.anchoredPosition = new Vector2(xCenter, axisH);
            g.GetComponent<Image>().color = guide;
        }

        // Scaling based on both series (only use completed months for current)
        float max = 0f;
        if (current12 != null)
        {
            for (int i = 0; i < Mathf.Min(months, completedMonths); i++) if (current12[i] > max) max = current12[i];
        }
        if (lastYear12 != null)
        {
            for (int i = 0; i < months; i++) if (lastYear12[i] > max) max = lastYear12[i];
        }
        if (max <= 0f) max = 1f;

        // Bars (fixed slots)
        for (int i = 0; i < months; i++)
        {
            if (i >= completedMonths || current12 == null) continue; // only full months
            float v = current12[i];
            float xCenter = XCenter(i);
            float h = Mathf.Clamp(chartH * (v / max), 0f, chartH);
            xCenter = Mathf.Clamp(xCenter, left + barW * 0.5f, innerRight - barW * 0.5f);
            var go = new GameObject($"Bar_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(bars, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(barW, h);
            rt.anchoredPosition = new Vector2(xCenter, axisH);
            var img = go.GetComponent<Image>(); img.color = barColor;
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

        // Month axis labels
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

        // Stretch container to area (no ContentSizeFitter)
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
            // Fallback: draw only current if lengths mismatch
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

        // Stretch containers to area (no ContentSizeFitter)
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

        // Build prev (behind)
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

        // Build current (front)
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

        // Create root Weather group if missing
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

        // Force the Weather root to fill the available width
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

                // Keep height and clip bars
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
        // Top row
        cashText   = cashText   ? cashText   : transform.Find("TopRow/CashText")  ?.GetComponent<TMP_Text>();
        marketText = marketText ? marketText : transform.Find("TopRow/MarketText")?.GetComponent<TMP_Text>();
        brandText  = brandText  ? brandText  : transform.Find("TopRow/BrandText") ?.GetComponent<TMP_Text>();
        dayText    = dayText    ? dayText    : transform.Find("TopRow/DayText")   ?.GetComponent<TMP_Text>();

        // Groups
        revenueChartArea = revenueChartArea ? revenueChartArea : transform.Find("Revenue/ChartArea") as RectTransform;
        revenueTitle     = revenueTitle     ? revenueTitle     : transform.Find("Revenue/Title")     ?.GetComponent<TMP_Text>();
        revenueAxisHint  = revenueAxisHint  ? revenueAxisHint  : transform.Find("Revenue/AxisHint")  ?.GetComponent<TMP_Text>();

        bottlesChartArea = bottlesChartArea ? bottlesChartArea : transform.Find("Bottles/ChartArea") as RectTransform;
        bottlesTitle     = bottlesTitle     ? bottlesTitle     : transform.Find("Bottles/Title")     ?.GetComponent<TMP_Text>();
        bottlesAxisHint  = bottlesAxisHint  ? bottlesAxisHint  : transform.Find("Bottles/AxisHint")  ?.GetComponent<TMP_Text>();

        // Weather
        weatherTempArea = weatherTempArea ? weatherTempArea : transform.Find("Weather/Temp/ChartArea") as RectTransform;
        weatherTempTitle = weatherTempTitle ? weatherTempTitle : transform.Find("Weather/Temp/Title")?.GetComponent<TMP_Text>();
        weatherRainArea = weatherRainArea ? weatherRainArea : transform.Find("Weather/Rain/ChartArea") as RectTransform;
        weatherRainTitle = weatherRainTitle ? weatherRainTitle : transform.Find("Weather/Rain/Title")?.GetComponent<TMP_Text>();
        weatherSunArea = weatherSunArea ? weatherSunArea : transform.Find("Weather/Sun/ChartArea") as RectTransform;
        weatherSunTitle = weatherSunTitle ? weatherSunTitle : transform.Find("Weather/Sun/Title")?.GetComponent<TMP_Text>();
    }
}