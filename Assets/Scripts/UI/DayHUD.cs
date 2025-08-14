using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DayHUD : MonoBehaviour
{
    [Header("Day/Season UI")]
    [SerializeField] TMP_Text dayLabel;      // Y{year} • Day {d}
    [SerializeField] Image dayProgress;      // Image Type=Filled, Fill Method=Horizontal
    [SerializeField] TMP_Text seasonLabel;   // Spring/Summer/Autumn/Winter
    [SerializeField] Image seasonDot;        // color indicator

    [Header("Cash UI")] 
    [SerializeField] TMP_Text cashText;      // left of day graphic in layout

    [Header("Options")] 
    [SerializeField] bool showSeason = true;
    [SerializeField] string[] seasonNames = { "Spring", "Summer", "Autumn", "Winter" };

    void Awake()
    {
        AutoCache();
        if (seasonDot) seasonDot.raycastTarget = false;
        if (dayProgress) dayProgress.raycastTarget = false;
        if (dayLabel) dayLabel.raycastTarget = false;
        if (seasonLabel) seasonLabel.raycastTarget = false;
        if (cashText) cashText.raycastTarget = false;
    }

    void OnEnable()
    {
        RefreshText();
        if (TimeController.I) TimeController.I.OnNewDay += RefreshText;
    }
    void OnDisable()
    {
        if (TimeController.I) TimeController.I.OnNewDay -= RefreshText;
    }

    void Update()
    {
        if (TimeController.I && dayProgress)
            dayProgress.fillAmount = TimeController.I.DayProgress01;
    }

    void RefreshText()
    {
        // Cash
        if (cashText)
        {
            if (EconomySystem.I)
                cashText.text = $"Cash: ${EconomySystem.I.Cash:n0}";
            else
                cashText.text = "Cash: ?";
        }

        if (!TimeController.I) return;

        int year = TimeController.I.Year + 1;
        int doy  = TimeController.I.DayOfYear + 1;

        // Day label BELOW the progress bar (layout handled in scene)
        if (dayLabel)
            dayLabel.text = $"Y{year} • Day {doy}";

        // Season label BELOW the season dot
        if (showSeason)
        {
            string season = GetSeasonName(doy);
            if (seasonLabel) seasonLabel.text = season;

            if (seasonDot)
            {
                int si = GetSeasonIndex(doy);
                Color c = si switch
                {
                    0 => new Color(0.70f, 0.88f, 0.70f, 1f), // Spring
                    1 => new Color(0.99f, 0.86f, 0.58f, 1f), // Summer
                    2 => new Color(0.95f, 0.78f, 0.64f, 1f), // Autumn
                    _ => new Color(0.78f, 0.86f, 0.96f, 1f), // Winter
                };
                seasonDot.color = c;
            }
        }
        else
        {
            if (seasonLabel) seasonLabel.text = string.Empty;
        }
    }

    int GetSeasonIndex(int dayOfYear)
    {
        int dpy = GameConfigHolder.Instance ? GameConfigHolder.Instance.Config.daysPerYear : 360;
        float quarter = dpy / 4f;
        int idx = Mathf.Clamp(Mathf.FloorToInt((dayOfYear) / quarter), 0, 3);
        return idx;
    }
    string GetSeasonName(int dayOfYear)
    {
        int idx = GetSeasonIndex(dayOfYear);
        if (seasonNames != null && seasonNames.Length == 4) return seasonNames[idx];
        return idx switch { 0 => "Spring", 1 => "Summer", 2 => "Autumn", _ => "Winter" };
    }

    private void AutoCache()
    {
        // Try conventional names; keeps older scenes working
        if (!dayProgress)  dayProgress  = transform.Find("DayProgress") ?.GetComponent<Image>();
        if (!dayLabel)     dayLabel     = transform.Find("DayLabel")    ?.GetComponent<TMP_Text>()
                                        ?? transform.Find("DayText")    ?.GetComponent<TMP_Text>();
        if (!seasonDot)    seasonDot    = transform.Find("SeasonDot")   ?.GetComponent<Image>();
        if (!seasonLabel)  seasonLabel  = transform.Find("SeasonLabel") ?.GetComponent<TMP_Text>();
        if (!cashText)     cashText     = transform.Find("CashText")    ?.GetComponent<TMP_Text>();
    }
}
