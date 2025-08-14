using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Stats screen UI
/// - Top: Cash, Market, Brand, Day
/// - Revenue bars (last year, 12 buckets)
/// - Bottles bars (last 180 days, 24 buckets)
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
        BuildAll();
        if (TimeController.I) TimeController.I.OnNewDay += BuildAll;
    }
    void OnDisable()
    {
        if (TimeController.I) TimeController.I.OnNewDay -= BuildAll;
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
    }

    // --- chart helpers ---

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

        // Clear previous bars
        for (int i = area.childCount - 1; i >= 0; i--) Destroy(area.GetChild(i).gameObject);

        if (values == null || values.Length == 0) return;

        float max = 0f;
        for (int i = 0; i < values.Length; i++) if (values[i] > max) max = values[i];
        if (max <= 0f) max = 1f;

        var rect = area.rect;
        float availableW = Mathf.Max(0f, rect.width - (values.Length + 1) * barSpacing);
        float w = values.Length > 0 ? availableW / values.Length : 0f;
        w = Mathf.Clamp(w, minBarWidth, maxBarWidth);

        float x = barSpacing;
        for (int i = 0; i < values.Length; i++)
        {
            float h = (rect.height - chartTopPadding) * (values[i] / max);
            var go = new GameObject($"Bar_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(area, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<Image>();
            img.color = color;

            x += w + barSpacing;
        }
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
    }
}