using System;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VineyardDetailPanel : MonoBehaviour
{
    [Header("Core UI")]
    [SerializeField] TMP_Text title;                 // "Plot 3"
    [SerializeField] TMP_Text info;                  // short status line
    [SerializeField] TMP_Dropdown varietyDropdown;
    [SerializeField] TMP_Dropdown yeastDropdown;
    [SerializeField] Button buyBtn;
    [SerializeField] Button plantBtn;
    [SerializeField] Button harvestBtn;
    [SerializeField] Button closeBtn;

    [Header("Plot details UI")]
    [SerializeField] TMP_Text plotHeaderText;        // "Site & Vines"
    [SerializeField] TMP_Text plotStatsText;         // multiline stats

    int index = -1;
    Action onChange;
    bool dropdownsInit;
    bool hasYeastOptions;

    void Reset()      { AutoCache(); }
    void OnValidate() { if (!Application.isPlaying) AutoCache(); }

    void OnEnable()
    {
        AutoCache();
        if (TimeController.I) TimeController.I.OnNewDay += RefreshAll;
        RefreshAll();
    }

    void OnDisable()
    {
        if (TimeController.I) TimeController.I.OnNewDay -= RefreshAll;
    }

    public void Open(int tileIndex, Action onChange = null)
    {
        index = tileIndex;
        this.onChange = onChange;
        gameObject.SetActive(true);
        dropdownsInit = false;
        PopulateDropdowns();
        RefreshAll();
    }

    public void Close() => gameObject.SetActive(false);

    // ---------------------- UI Refresh ----------------------

    void RefreshAll()
    {
        PopulateDropdowns();
        if (index < 0 || VineyardSystem.I == null) return;

        var s = VineyardSystem.I.GetState(index);
        if (s == null)
        {
            if (title) title.text = "Plot " + (index + 1);
            if (info) info.text = "Unknown plot";
            if (plotHeaderText) plotHeaderText.text = "Site & Vines";
            if (plotStatsText) plotStatsText.text = "-";
            ToggleButtons(false, false, false, false);
            return;
        }

        bool owned   = s.owned;
        bool planted = !string.IsNullOrEmpty(s.plantedVariety);

        if (title) title.text = "Plot " + (index + 1);
        if (info)
        {
            if (!owned)            info.text = "Not owned";
            else if (!planted)     info.text = "Owned • Empty";
            else                   info.text = s.plantedVariety + " • D" + s.daysSincePlanting;
        }

        // Show/Hide dropdowns by state
        if (varietyDropdown) varietyDropdown.gameObject.SetActive(owned && !planted);
        if (yeastDropdown)   yeastDropdown.gameObject.SetActive(owned && planted && hasYeastOptions);

        BuildPlotStats(s);

        // Buttons
        if (buyBtn)     { buyBtn.gameObject.SetActive(!owned);         buyBtn.onClick.RemoveAllListeners();     buyBtn.onClick.AddListener(OnBuy); }
        if (plantBtn)   { plantBtn.gameObject.SetActive(owned && !planted); plantBtn.onClick.RemoveAllListeners(); plantBtn.onClick.AddListener(OnPlant); }
        if (harvestBtn) { harvestBtn.gameObject.SetActive(owned && planted); harvestBtn.onClick.RemoveAllListeners(); harvestBtn.onClick.AddListener(OnHarvest); }
        if (closeBtn)   { closeBtn.onClick.RemoveAllListeners();       closeBtn.onClick.AddListener(Close); }
    }

    void ToggleButtons(bool buy, bool plant, bool harvest, bool close)
    {
        if (buyBtn)     buyBtn.gameObject.SetActive(buy);
        if (plantBtn)   plantBtn.gameObject.SetActive(plant);
        if (harvestBtn) harvestBtn.gameObject.SetActive(harvest);
        if (closeBtn)   closeBtn.gameObject.SetActive(close);
    }

    void BuildPlotStats(VineyardTileState s)
    {
        if (plotHeaderText) plotHeaderText.text = "Site & Vines";

        // Site details (robust reflection to tolerate missing fields)
        string soil        = GetAny(s, "soilType", GetAny(s, "soil", "-")?.ToString());
        string orientation = GetAny(s, "orientation", GetAny(s, "aspect", "-"));
        string slope       = GetAny(s, "slope", "-");
        string irrigation  = GetAny(s, "irrigation", "-");

        // Humidity: prefer per-plot soil moisture; fallback to weather humidity
        float soilMoist = GetAny(s, "soilMoisture", GetAny(s, "humidity", -1f));
        if (soilMoist < 0f)
        {
            try
            {
                var ws = WeatherSystem.I;
                if (ws != null)
                {
                    object today = ws.Today;
                    if (today != null)
                    {
                        soilMoist = GetAny(today, "humidity", -1f);
                        if (soilMoist < 0f) soilMoist = GetAny(today, "relativeHumidity", -1f);
                        if (soilMoist < 0f) soilMoist = GetAny(today, "humidityPct", -1f);
                        if (soilMoist < 0f) soilMoist = GetAny(today, "rh", -1f);
                        if (soilMoist > 0f && soilMoist <= 1f) soilMoist *= 100f; // normalize 0..1 -> %
                    }
                }
            }
            catch { soilMoist = -1f; }
        }
        string humidityStr = (soilMoist >= 0f) ? string.Format("{0:0}% RH", soilMoist) : "-";

        // Vine metrics (if planted)
        bool planted = !string.IsNullOrEmpty(s.plantedVariety);
        float brix   = planted ? s.brix : 0f;
        float pH     = planted ? GetAny(s, "pH", GetAny(s, "acidity", 0f)) : 0f;   // pH preferred
        float ta     = GetAny(s, "acidity", -1f);                                  // TA if present
        float phen   = planted ? GetAny(s, "phenolic", 0f) : 0f;
        float ready  = VineyardSystem.I.GetReadiness01(index);

        var sb = new StringBuilder();
        sb.AppendLine("<b>Site</b>");
        sb.AppendLine("• Soil: " + Pretty(soil));
        sb.AppendLine("• Orientation: " + Pretty(orientation));
        sb.AppendLine("• Slope: " + Pretty(slope));
        sb.AppendLine("• Irrigation: " + Pretty(irrigation));
        sb.AppendLine("• Humidity: " + humidityStr);

        if (planted)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Vines</b>");
            sb.AppendLine("• Variety: " + s.plantedVariety);
            sb.AppendLine("• Days since planting: " + s.daysSincePlanting);
            sb.AppendLine("• Brix: " + brix.ToString("0.0"));
            if (ta >= 0f) sb.AppendLine("• Acidity (TA): " + ta.ToString("0.00") + " g/L");
            if (pH > 0f)  sb.AppendLine("• pH: " + pH.ToString("0.00"));
            sb.AppendLine("• Phenolic: " + phen.ToString("0"));
            sb.AppendLine("• Readiness: " + (ready * 100f).ToString("0") + "%");
        }

        if (plotStatsText) plotStatsText.text = sb.ToString();
    }

    string Pretty(object v)
    {
        if (v == null) return "-";
        var s = v.ToString();
        return string.IsNullOrWhiteSpace(s) ? "-" : s;
    }

    T GetAny<T>(object obj, string name, T fallback)
    {
        if (obj == null) return fallback;
        try
        {
            var f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && typeof(T).IsAssignableFrom(f.FieldType)) return (T)f.GetValue(obj);
            var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && typeof(T).IsAssignableFrom(p.PropertyType)) return (T)p.GetValue(obj);
            // numeric conversion helper for floats
            if (p != null && p.CanRead && typeof(T) == typeof(float) && p.PropertyType.IsValueType)
                return (T)Convert.ChangeType(p.GetValue(obj), typeof(T));
        }
        catch { }
        return fallback;
    }

    // ---------------------- Button Handlers ----------------------

    void OnBuy()
    {
        if (VineyardSystem.I != null && VineyardSystem.I.TryBuyPlot(index))
        {
            RefreshAll();
            onChange?.Invoke();
        }
    }

    void OnPlant()
    {
        string variety = (varietyDropdown && varietyDropdown.options.Count > 0)
            ? varietyDropdown.options[varietyDropdown.value].text
            : null;

        if (string.IsNullOrWhiteSpace(variety) || variety == "— no varieties —")
        {
            Debug.LogWarning("Plant: No variety selected / no varieties configured.");
            return;
        }

        var s = VineyardSystem.I?.GetState(index);
        if (s == null) { Debug.LogWarning("Plant: invalid plot index."); return; }
        if (!s.owned)  { Debug.LogWarning("Plant: plot not owned."); return; }
        if (!string.IsNullOrEmpty(s.plantedVariety))
        {
            Debug.LogWarning("Plant: plot already planted.");
            return;
        }

        if (VineyardSystem.I != null && VineyardSystem.I.TryPlant(index, variety))
        {
            RefreshAll();
            onChange?.Invoke();
        }
        else
        {
            Debug.LogWarning("Plant failed for variety '" + variety + "'. Check that this variety exists and you have enough cash.");
            RefreshAll();
        }
    }

    void OnHarvest()
    {
        if (VineyardSystem.I == null) return;

        // Optional: block if not in harvest window
        float readiness;
        if (!VineyardSystem.I.CanHarvest(index, out readiness))
        {
            Debug.Log("Not in harvest window yet.");
            return;
        }

        var batch = VineyardSystem.I.Harvest(index);
        if (batch == null) return;

        // Selected yeast (optional)
        string selYeast = GetSelectedYeastName();
        if (!string.IsNullOrWhiteSpace(selYeast))
            TryAttachYeastToBatch(batch, selYeast);

        var ps = ProductionSystem.I;
        float lpk = GetLitersPerKg();
        int volL = Mathf.RoundToInt(Mathf.Max(0, batch.kg) * lpk);

        bool handedToProd = false;
        int chosenIndex = -1;
        int chosenCap = 0;
        string failReason = null;

        if (ps != null)
        {
            int usedLiters;
            handedToProd = ps.TryReceiveHarvestWithLog(batch, selYeast ?? string.Empty, out chosenIndex, out chosenCap, out usedLiters);
            if (!handedToProd)
                failReason = "No empty tank large enough or all occupied.";
        }
        else
        {
            failReason = "No ProductionSystem present.";
        }

        if (handedToProd)
        {
            Debug.Log(string.Format(
                "Harvest {0:n0} kg (~{1:n0} L @ {2:0.00} L/kg) -> Tank #{3} (cap {4:n0} L). Starting fermentation.",
                batch.kg, volL, lpk, chosenIndex, chosenCap));
        }
        else
        {
            int revenue = Mathf.RoundToInt(Mathf.Max(0, batch.kg) * 2f); // crude wholesale
            if (EconomySystem.I != null)
            {
                if (!TryCreditEconomy(revenue))
                    Debug.LogWarning("EconomySystem: couldn't credit revenue automatically.");
            }
            Debug.Log(string.Format(
                "Harvest {0:n0} kg (~{1:n0} L @ {2:0.00} L/kg) not assigned: {3}. Sold wholesale (${4:n0}).",
                batch.kg, volL, lpk, (failReason ?? "Unknown reason"), revenue));
        }

        RefreshAll();
        onChange?.Invoke();
    }

    // Try to credit revenue on EconomySystem via common method/property/field names
    bool TryCreditEconomy(int amount)
    {
        var eco = EconomySystem.I;
        if (eco == null) return false;
        try
        {
            var type = eco.GetType();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            string[] preferred = { "AddCash", "Credit", "Deposit", "AddRevenue", "Add", "IncreaseCash", "ModifyCash", "ChangeCash", "Earn", "Income" };
            foreach (var name in preferred)
            {
                var m = methods.FirstOrDefault(x =>
                    x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    x.GetParameters().Length == 1 &&
                    x.GetParameters()[0].ParameterType == typeof(int));
                if (m != null) { m.Invoke(eco, new object[] { amount }); return true; }
            }

            var m2 = methods.FirstOrDefault(x =>
                x.GetParameters().Length == 1 &&
                x.GetParameters()[0].ParameterType == typeof(int) &&
                (x.Name.ToLower().Contains("cash") || x.Name.ToLower().Contains("revenue") ||
                 x.Name.ToLower().Contains("add")  || x.Name.ToLower().Contains("credit")));
            if (m2 != null) { m2.Invoke(eco, new object[] { amount }); return true; }

            var prop = type.GetProperty("Cash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanRead)
            {
                int cur = 0;
                try { cur = (int)Convert.ChangeType(prop.GetValue(eco), typeof(int)); } catch { }
                if (prop.CanWrite) { prop.SetValue(eco, cur + amount); return true; }
            }

            var field = type.GetField("cash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     ?? type.GetField("Cash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                int cur = 0;
                try { cur = (int)Convert.ChangeType(field.GetValue(eco), typeof(int)); } catch { }
                field.SetValue(eco, cur + amount);
                return true;
            }
        }
        catch { }
        return false;
    }

    // ---------------------- Dropdowns ----------------------

    void PopulateDropdowns()
    {
        if (dropdownsInit) return;

        // Varieties from VineyardSystem
        if (varietyDropdown)
        {
            var options = new List<TMP_Dropdown.OptionData>();
            var vs = VineyardSystem.I;
            if (vs != null && vs.Varieties != null && vs.Varieties.Length > 0)
            {
                foreach (var v in vs.Varieties)
                    if (v != null && !string.IsNullOrWhiteSpace(v.varietyName))
                        options.Add(new TMP_Dropdown.OptionData(v.varietyName));
            }
            varietyDropdown.options.Clear();
            if (options.Count > 0)
            {
                varietyDropdown.AddOptions(options);
                varietyDropdown.value = 0;
                varietyDropdown.RefreshShownValue();
                varietyDropdown.interactable = true;
            }
            else
            {
                varietyDropdown.AddOptions(new List<TMP_Dropdown.OptionData> {
                    new TMP_Dropdown.OptionData("— no varieties —")
                });
                varietyDropdown.value = 0;
                varietyDropdown.RefreshShownValue();
                varietyDropdown.interactable = false;
            }
        }

        // Yeasts from GameConfig (if present)
        if (yeastDropdown)
        {
            var yeastNames = TryGetYeastNamesFromConfig();
            hasYeastOptions = yeastNames.Count > 0;
            yeastDropdown.options.Clear();
            if (yeastNames.Count > 0)
            {
                foreach (var n in yeastNames) yeastDropdown.options.Add(new TMP_Dropdown.OptionData(n));
                yeastDropdown.value = 0;
                yeastDropdown.RefreshShownValue();
                yeastDropdown.interactable = true;
                yeastDropdown.gameObject.SetActive(true);
            }
            else
            {
                yeastDropdown.gameObject.SetActive(false);
            }
        }

        dropdownsInit = true;
    }

    List<string> TryGetYeastNamesFromConfig()
    {
        var list = new List<string>();
        var holder = GameConfigHolder.Instance;
        if (holder == null || holder.Config == null) return list;

        var cfg = holder.Config;
        var t = cfg.GetType();
        var fld = t.GetField("yeasts", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var prop = t.GetProperty("yeasts", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        object arr = (fld != null) ? fld.GetValue(cfg) : (prop != null ? prop.GetValue(cfg) : null);
        if (arr is System.Collections.IEnumerable en)
        {
            foreach (var item in en)
            {
                if (item == null) continue;
                var it = item.GetType();
                string name =
                    (it.GetField("yeastName")?.GetValue(item)?.ToString()) ??
                    (it.GetProperty("yeastName")?.GetValue(item)?.ToString()) ??
                    (it.GetField("name")?.GetValue(item)?.ToString()) ??
                    (it.GetProperty("name")?.GetValue(item)?.ToString());
                if (!string.IsNullOrWhiteSpace(name)) list.Add(name);
            }
        }
        return list;
    }

    string GetSelectedYeastName()
    {
        if (!yeastDropdown || !yeastDropdown.gameObject.activeInHierarchy) return null;
        if (yeastDropdown.options == null || yeastDropdown.options.Count == 0) return null;
        var opt = yeastDropdown.options[Mathf.Clamp(yeastDropdown.value, 0, yeastDropdown.options.Count - 1)];
        var txt = opt != null ? opt.text : null;
        if (string.IsNullOrWhiteSpace(txt) || txt.StartsWith("—")) return null;
        return txt;
    }

    void TryAttachYeastToBatch(object batch, string yeastName)
    {
        if (batch == null) return;
        var bt = batch.GetType();

        // 1) Name fields/properties
        string[] nameCandidates = { "yeastName", "YeastName", "yeast", "Yeast", "selectedYeast", "SelectedYeast" };
        foreach (var n in nameCandidates)
        {
            var f = bt.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && (f.FieldType == typeof(string) || f.FieldType == typeof(object))) { f.SetValue(batch, yeastName); return; }
            var p = bt.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite && (p.PropertyType == typeof(string) || p.PropertyType == typeof(object))) { p.SetValue(batch, yeastName); return; }
        }

        // 2) Object reference (YeastSO) by name from GameConfig
        var holder = GameConfigHolder.Instance;
        var cfg = holder ? holder.Config : null;
        object yeastSO = null;
        if (cfg != null)
        {
            try
            {
                var ct = cfg.GetType();
                var fld = ct.GetField("yeasts", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var prop = ct.GetProperty("yeasts", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object arr = (fld != null) ? fld.GetValue(cfg) : (prop != null ? prop.GetValue(cfg) : null);
                if (arr is System.Collections.IEnumerable en)
                {
                    foreach (var item in en)
                    {
                        if (item == null) continue;
                        var it = item.GetType();
                        string name =
                            (it.GetField("yeastName")?.GetValue(item)?.ToString()) ??
                            (it.GetProperty("yeastName")?.GetValue(item)?.ToString()) ??
                            (it.GetField("name")?.GetValue(item)?.ToString()) ??
                            (it.GetProperty("name")?.GetValue(item)?.ToString());
                        if (!string.IsNullOrWhiteSpace(name) && string.Equals(name, yeastName, StringComparison.OrdinalIgnoreCase))
                        {
                            yeastSO = item; break;
                        }
                    }
                }
            }
            catch { }
        }

        if (yeastSO != null)
        {
            string[] soCandidates = { "yeast", "Yeast", "yeastSO", "YeastSO" };
            foreach (var n in soCandidates)
            {
                var f = bt.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType.IsInstanceOfType(yeastSO)) { f.SetValue(batch, yeastSO); return; }
                var p = bt.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanWrite && p.PropertyType.IsInstanceOfType(yeastSO)) { p.SetValue(batch, yeastSO); return; }
            }
        }

        // 3) Fallback numeric id if batch expects it
        int id = 0;
        string[] idCandidates = { "yeastId", "YeastId", "yeastIndex", "YeastIndex" };
        foreach (var n in idCandidates)
        {
            var f = bt.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(int)) { f.SetValue(batch, id); return; }
            var p = bt.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite && p.PropertyType == typeof(int)) { p.SetValue(batch, id); return; }
        }
    }

    // ---------------------- Misc helpers ----------------------

    float GetLitersPerKg()
    {
        float lpk = 0.7f;
        try
        {
            var holder = GameConfigHolder.Instance;
            var cfg = holder ? holder.Config : null;
            if (cfg != null)
            {
                var t = cfg.GetType();
                var fld = t.GetField("litersPerKg", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var prop = t.GetProperty("litersPerKg", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object val = (fld != null) ? fld.GetValue(cfg) : (prop != null ? prop.GetValue(cfg) : null);
                if (val != null) lpk = (float)Convert.ChangeType(val, typeof(float));
            }
        }
        catch { }
        return Mathf.Max(0.1f, lpk);
    }

    void AutoCache()
    {
        if (!title)           title           = transform.Find("Title")?.GetComponent<TMP_Text>();
        if (!info)            info            = transform.Find("Info")?.GetComponent<TMP_Text>();
        if (!varietyDropdown) varietyDropdown = transform.Find("VarietyDropdown")?.GetComponent<TMP_Dropdown>();
        if (!yeastDropdown)   yeastDropdown   = transform.Find("YeastDropdown")?.GetComponent<TMP_Dropdown>();
        if (!buyBtn)          buyBtn          = transform.Find("BuyBtn")?.GetComponent<Button>();
        if (!plantBtn)        plantBtn        = transform.Find("PlantBtn")?.GetComponent<Button>();
        if (!harvestBtn)      harvestBtn      = transform.Find("HarvestBtn")?.GetComponent<Button>();
        if (!closeBtn)        closeBtn        = transform.Find("CloseBtn")?.GetComponent<Button>();

        if (!plotHeaderText)  plotHeaderText  = transform.Find("PlotHeaderText")?.GetComponent<TMP_Text>();
        if (!plotStatsText)   plotStatsText   = transform.Find("PlotStatsText")?.GetComponent<TMP_Text>();
    }
}