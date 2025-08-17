using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

[DefaultExecutionOrder(500)]
public class InventoryPanel : MonoBehaviour
{
    [Header("Top")]
    [SerializeField] TMP_Text cashText;

    [Header("List")]
    [SerializeField] RectTransform stockContent;
    [SerializeField] InventoryItemUI stockItemPrefab;

    [Header("Sell Modal")]
    [SerializeField] GameObject sellModal;
    [SerializeField] TMP_Text sellTitle;
    [SerializeField] TMP_InputField qtyInput;
    [SerializeField] TMP_Text maxText;
    [SerializeField] TMP_Text estText;
    [SerializeField] Button confirmSellBtn;
    [SerializeField] Button cancelSellBtn;

    // runtime
    private BottleEntryState selected;
    private string selectedId;
    private int selectedMax;
    private float unitPriceEst;

    private readonly List<InventoryItemUI> items = new();
    private bool _isRebuilding;
    private bool _scrollBuilt;

    // rebuild guarding (avoid list being rebuilt while interacting with modal)
    private bool _modalOpen;
    private bool _pendingRebuild;
    private float _rebuildAt;

    void Reset()      { AutoCache(); }
    void OnValidate() { if (!Application.isPlaying) AutoCache(); }

    void OnEnable()
    {
        AutoCache();
        RefreshTop();
        Rebuild();
        if (TimeController.I) TimeController.I.OnNewDay += OnNewDay;
    }
    void OnDisable()
    {
        if (TimeController.I) TimeController.I.OnNewDay -= OnNewDay;
    }

    void OnNewDay()
    {
        RefreshTop();
        if (_modalOpen)
        {
            _pendingRebuild = true;
            _rebuildAt = Time.time + 0.05f; // slight debounce
        }
        else
        {
            Rebuild();
        }
    }

    void Update()
    {
        if (_pendingRebuild && !_modalOpen && Time.time >= _rebuildAt)
        {
            _pendingRebuild = false;
            Rebuild();
        }
    }

    public void Rebuild()
    {
        if (_isRebuilding) return;
        if (stockContent == null || stockItemPrefab == null)
        {
            Debug.LogError("InventoryPanel: stockContent or stockItemPrefab not assigned.", this);
            return;
        }

        _isRebuilding = true;

        var entries = InventorySystem.I?.Serialize() ?? Array.Empty<BottleEntryState>();

        // Ensure pool size
        while (items.Count < entries.Length)
        {
            var item = Instantiate(stockItemPrefab, stockContent);
            EnsureItemSizing(item.gameObject);
            items.Add(item);
        }

        // Update visible rows
        for (int i = 0; i < entries.Length; i++)
        {
            var row = items[i];
            if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
            row.Setup(entries[i], OpenSell);
        }

        // Hide extras
        for (int i = entries.Length; i < items.Count; i++)
        {
            if (items[i].gameObject.activeSelf) items[i].gameObject.SetActive(false);
        }

        _isRebuilding = false;
    }

    private void RefreshTop()
    {
        if (cashText) cashText.text = EconomySystem.I ? $"Cash: ${EconomySystem.I.Cash:n0}" : "Cash: ?";
    }

    // ---------- Sell Modal ----------

    public void OpenSell(BottleEntryState entry)
    {
        selected = entry;
        selectedMax = Mathf.Max(0, GetProp(entry, "bottles", 0));

        string variety   = GetProp(entry, "varietyName", "Wine");
        int vintageRaw   = GetProp(entry, "vintageYear", 0);
        int vintage      = ResolveVintageYear(vintageRaw);

        // Always build a safe ID (avoids "|0" which fails lookup in backend)
        selectedId = BuildIdForEntry(entry);

        Debug.Log($"InventoryPanel.OpenSell: id='{selectedId}' variety='{variety}' vintage={vintage} max={selectedMax}", this);

        sellTitle?.SetText($"Sell {variety} {vintage}");
        maxText?.SetText($"In stock: {selectedMax}");

        // Best-effort unit price estimate
        unitPriceEst = GetProp(entry, "marketPrice", GetProp(entry, "pricePerBottle", 0f));
        if (unitPriceEst <= 0f)
        {
            // try derived: quality * marketIndex * some factor (fallback)
            float q = GetProp(entry, "qualityScore", GetProp(entry, "quality", 0f));
            float m = EconomySystem.I ? EconomySystem.I.MarketIndex : 1f;
            unitPriceEst = Mathf.Max(1f, (10f + q * 2.5f) * m);
        }

        int defaultQty = selectedMax;
        if (qtyInput)
        {
            qtyInput.onValueChanged.RemoveAllListeners();
            qtyInput.text = defaultQty.ToString();
            qtyInput.onValueChanged.AddListener(_ => UpdateEstimate());
        }

        if (confirmSellBtn)
        {
            confirmSellBtn.onClick.RemoveAllListeners();
            confirmSellBtn.onClick.AddListener(() =>
            {
                int q = ParseQty(qtyInput?.text, 0, selectedMax);
                if (q <= 0) { Toast("Enter a quantity > 0"); return; }

                // Rebuild a safe ID at click time in case data changed
                var safeId = BuildIdForEntry(selected);
                if (TrySell(safeId, q, out int sold, out int revenue))
                {
                    Toast($"Sold {sold} for ${revenue:n0}");
                    CloseSell();
                    _modalOpen = false;
                    RefreshTop();
                    _pendingRebuild = true;
                    _rebuildAt = Time.time + 0.05f;
                }
                else
                {
                    Toast("Sell failed (no backend handler).");
                }
            });
        }
        if (cancelSellBtn)
        {
            cancelSellBtn.onClick.RemoveAllListeners();
            cancelSellBtn.onClick.AddListener(CloseSell);
        }

        UpdateEstimate();
        _modalOpen = true;
        if (sellModal) sellModal.SetActive(true);
    }

    public void CloseSell()
    {
        _modalOpen = false;
        if (sellModal) sellModal.SetActive(false);
    }

    private void UpdateEstimate()
    {
        int q = ParseQty(qtyInput?.text, 0, selectedMax);
        if (estText) estText.text = $"Est. revenue: ${(q * unitPriceEst):n0}";
        if (confirmSellBtn) confirmSellBtn.interactable = q > 0;
    }

    private int ParseQty(string s, int min, int max)
    {
        if (int.TryParse(s, out var v))
        {
            return Mathf.Clamp(v, min, max);
        }
        return 0;
    }

    // ---------- Backend: best-effort selling (reflection) ----------

    public bool TrySell(string id, int count, out int sold, out int revenue)
    {
        sold = 0; revenue = 0;

        // 0) Direct route – avoid reflection when possible
        if (InventorySystem.I != null)
        {
            try
            {
                // Preferred: exact id-based API
                if (InventorySystem.I.TrySell(id, count, out sold, out revenue))
                    return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"InventoryPanel.TrySell: direct TrySell(id,count) threw: {ex.Message}");
            }

            try
            {
                // Alternate: int Sell(id,count) returns revenue
                int rev = InventorySystem.I.Sell(id, count);
                if (rev > 0) { revenue = rev; sold = count; return true; }
            }
            catch {}

            try
            {
                // Fallback: if id encodes variety|vintage, route to existing variety/vintage Sell
                var parts = id.Split('|');
                if (parts.Length >= 2 && int.TryParse(parts[1], out var vint))
                {
                    int pricePerBottle;
                    int rev = InventorySystem.I.Sell(parts[0], vint, count, out pricePerBottle);
                    if (rev > 0) { revenue = rev; sold = count; return true; }
                }
            }
            catch {}
        }

        // 1) Reflection on InventorySystem – sell-like methods
        var inv = InventorySystem.I;
        if (inv != null)
        {
            var type = inv.GetType();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            // 1a) bool TrySell(string id, int count, out int sold, out int revenue)
            var m = methods.FirstOrDefault(x =>
                x.Name.IndexOf("sell", StringComparison.OrdinalIgnoreCase) >= 0 &&
                x.ReturnType == typeof(bool) &&
                MatchSig(x, new[] { typeof(string), typeof(int), typeof(int).MakeByRefType(), typeof(int).MakeByRefType() })
            );
            if (m != null)
            {
                object[] args = new object[] { id, count, 0, 0 };
                bool ok = (bool)m.Invoke(inv, args);
                if (ok) { sold = (int)args[2]; revenue = (int)args[3]; }
                return ok;
            }

            // 1b) int Sell(string id, int count) -> returns revenue
            m = methods.FirstOrDefault(x =>
                x.Name.IndexOf("sell", StringComparison.OrdinalIgnoreCase) >= 0 &&
                x.ReturnType == typeof(int) &&
                MatchSig(x, new[] { typeof(string), typeof(int) })
            );
            if (m != null)
            {
                int rev = (int)m.Invoke(inv, new object[] { id, count });
                if (rev > 0) { revenue = rev; sold = count; return true; }
            }

            // Debug what we do see
            var found = methods.Where(x => x.Name.IndexOf("sell", StringComparison.OrdinalIgnoreCase) >= 0)
                               .Select(x => $"{x.Name}({string.Join(",", x.GetParameters().Select(p => p.ParameterType.Name))}) -> {x.ReturnType.Name}");
            Debug.Log($"InventoryPanel.TrySell: InventorySystem sell-like methods = [{string.Join("; ", found)}]");
        }

        // 2) Try ActionAPI last – a bool Sell*(string id, int count)
        var api = FindFirstObjectByType<ActionAPI>();
        if (api)
        {
            var typeA = api.GetType();
            var m = typeA.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                         .FirstOrDefault(x => x.Name.StartsWith("Sell", StringComparison.OrdinalIgnoreCase) && x.ReturnType == typeof(bool) && MatchSig(x, new[] { typeof(string), typeof(int) }));
            if (m != null)
            {
                bool ok = (bool)m.Invoke(api, new object[] { id, count });
                if (ok) { sold = count; revenue = Mathf.RoundToInt(count * unitPriceEst); }
                return ok;
            }

            var foundA = typeA.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                               .Where(x => x.Name.StartsWith("Sell", StringComparison.OrdinalIgnoreCase))
                               .Select(x => $"{x.Name}({string.Join(",", x.GetParameters().Select(p => p.ParameterType.Name))}) -> {x.ReturnType.Name}");
            Debug.Log($"InventoryPanel.TrySell: ActionAPI sell-like methods = [{string.Join("; ", foundA)}]");
        }

        Debug.LogWarning("InventoryPanel: No selling method found on InventorySystem or ActionAPI.");
        return false;
    }

    private bool MatchSig(MethodInfo m, Type[] types)
    {
        var ps = m.GetParameters();
        if (ps.Length != types.Length) return false;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].ParameterType != types[i]) return false;
        }
        return true;
    }

    // ---------- Helpers ----------

    public T GetProp<T>(object obj, string name, T fallback)
    {
        if (obj == null) return fallback;

        // fields first
        var fi = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (fi != null && typeof(T).IsAssignableFrom(fi.FieldType))
            return (T)fi.GetValue(obj);

        // properties next
        var pi = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (pi != null && typeof(T).IsAssignableFrom(pi.PropertyType))
            return (T)pi.GetValue(obj);

        // allow common numeric conversions
        if (pi != null && pi.PropertyType.IsValueType && typeof(T) == typeof(float))
        {
            try { return (T)Convert.ChangeType(pi.GetValue(obj), typeof(T)); } catch { }
        }
        if (pi != null && pi.PropertyType.IsValueType && typeof(T) == typeof(int))
        {
            try { return (T)Convert.ChangeType(pi.GetValue(obj), typeof(T)); } catch { }
        }

        return fallback;
    }

    // --- Config/Vintage/ID helpers ---

    // Try to extract a reasonable "start year" from a ScriptableObject that looks like GameConfig.
    private int ExtractStartYearFromConfigObject(object cfgObj)
    {
        if (cfgObj == null) return 0;
        var t = cfgObj.GetType();

        // 1) Scan public instance INT fields with name patterns
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (f.FieldType != typeof(int)) continue;
            var n = f.Name.ToLowerInvariant();
            if ((n.Contains("start") && n.Contains("year")) || n == "startyear" || n == "startingyear" || n == "baseyear" || n == "yearstart" || n == "seedyear")
            {
                int val = (int)f.GetValue(cfgObj);
                if (val > 0) return val;
            }
        }

        // 2) Scan public instance INT properties with name patterns
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.PropertyType != typeof(int)) continue;
            var n = p.Name.ToLowerInvariant();
            if ((n.Contains("start") && n.Contains("year")) || n == "startyear" || n == "startingyear" || n == "baseyear" || n == "yearstart" || n == "seedyear")
            {
                try
                {
                    int val = (int)(p.GetValue(cfgObj) ?? 0);
                    if (val > 0) return val;
                }
                catch { }
            }
        }

        return 0;
    }

    // Find startYear from whatever field/property holds a GameConfig on the scene holder, or from any loaded GameConfig asset.
    private int GetStartYearFromConfig()
    {
        int start = 0;

        // Prefer a holder in-scene
        var holder = FindFirstObjectByType<GameConfigHolder>();
        if (holder != null)
        {
            // Try a public instance FIELD of type GameConfig
            var gf = holder.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => typeof(GameConfig).IsAssignableFrom(f.FieldType));
            if (gf != null)
            {
                var cfg = gf.GetValue(holder);
                start = ExtractStartYearFromConfigObject(cfg);
            }

            // Or a public instance PROPERTY of type GameConfig
            if (start <= 0)
            {
                var gp = holder.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => typeof(GameConfig).IsAssignableFrom(p.PropertyType));
                if (gp != null)
                {
                    var cfg = gp.GetValue(holder);
                    start = ExtractStartYearFromConfigObject(cfg);
                }
            }
        }

        // Last resort: any loaded GameConfig ScriptableObject
        if (start <= 0)
        {
            // Use name-based discovery to avoid a hard dependency on a 'startYear' field/property
            var anyConfigs = Resources.FindObjectsOfTypeAll<ScriptableObject>()
                                      .Where(o => o != null && o.GetType().Name == "GameConfig")
                                      .ToArray();
            foreach (var cfg in anyConfigs)
            {
                start = ExtractStartYearFromConfigObject(cfg);
                if (start > 0) break;
            }
        }

        return start;
    }

    private int ResolveVintageYear(int candidate)
    {
        if (candidate > 0) return candidate;

        int startYear = GetStartYearFromConfig();
        int year = TimeController.I ? TimeController.I.Year : 0; // if 0-based, startYear+Year => calendar
        int guess = (startYear > 0) ? startYear + year : year;
        if (guess <= 0) guess = 1;
        return guess;
    }

    private string BuildIdForEntry(BottleEntryState e)
    {
        if (e == null) return null;
        string variety = GetProp(e, "varietyName", "Wine");
        int vint = ResolveVintageYear(GetProp(e, "vintageYear", 0));
        return $"{variety}|{vint}";
    }

    private void AutoCache()
    {
        // Top
        cashText = cashText ? cashText : transform.Find("TopRow/CashText")?.GetComponent<TMP_Text>();

        // List
        var stockScroll = transform.Find("StockScroll");
        if (stockScroll)
        {
            var sr = stockScroll.GetComponent<ScrollRect>();
            if (!sr) sr = stockScroll.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;

            if (!_scrollBuilt)
            {
                var vpt = sr.viewport ? sr.viewport : stockScroll.Find("Viewport") as RectTransform;
                if (!vpt && !Application.isPlaying)
                {
                    // In edit-time only, create missing viewport
                    var go = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
                    vpt = go.GetComponent<RectTransform>();
                    vpt.SetParent(stockScroll, false);
                }
                if (vpt)
                {
                    vpt.anchorMin = Vector2.zero; vpt.anchorMax = Vector2.one; vpt.offsetMin = Vector2.zero; vpt.offsetMax = Vector2.zero;
                    sr.viewport = vpt;

                    var ct = sr.content ? sr.content : vpt.Find("Content") as RectTransform;
                    if (!ct && !Application.isPlaying)
                    {
                        var go = new GameObject("Content", typeof(RectTransform));
                        ct = go.GetComponent<RectTransform>();
                        ct.SetParent(vpt, false);
                    }
                    if (ct)
                    {
                        ct.anchorMin = Vector2.zero; ct.anchorMax = Vector2.one; ct.offsetMin = Vector2.zero; ct.offsetMax = Vector2.zero;
                        var vlg = ct.GetComponent<VerticalLayoutGroup>() ?? ct.gameObject.AddComponent<VerticalLayoutGroup>();
                        vlg.spacing = 8; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
                        var fit = ct.GetComponent<ContentSizeFitter>() ?? ct.gameObject.AddComponent<ContentSizeFitter>();
                        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                        sr.content = ct;
                    }
                }
                _scrollBuilt = true; // do not create again at runtime
            }

            stockContent = sr.content ? sr.content : stockContent;
            var le = stockScroll.GetComponent<LayoutElement>() ?? stockScroll.gameObject.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
        }

        // Sell modal (paths left as-is to match your current hierarchy)
        sellModal      = sellModal      ? sellModal      : transform.Find("SellModal")?.gameObject;
        sellTitle      = sellTitle      ? sellTitle      : transform.Find("SellModal/Title")?.GetComponent<TMP_Text>();
        qtyInput       = qtyInput       ? qtyInput       : transform.Find("SellModal/QtyInput")?.GetComponent<TMP_InputField>();
        maxText        = maxText        ? maxText        : transform.Find("SellModal/MaxText")?.GetComponent<TMP_Text>();
        estText        = estText        ? estText        : transform.Find("SellModal/EstText")?.GetComponent<TMP_Text>();
        confirmSellBtn = confirmSellBtn ? confirmSellBtn : transform.Find("SellModal/ConfirmBtn")?.GetComponent<Button>();
        cancelSellBtn  = cancelSellBtn  ? cancelSellBtn  : transform.Find("SellModal/CancelBtn")?.GetComponent<Button>();

        // Default hide modal
        if (sellModal) sellModal.SetActive(false);
    }

    private void EnsureItemSizing(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt && rt.sizeDelta.y < 140f) rt.sizeDelta = new Vector2(rt.sizeDelta.x, 160f);
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (le.minHeight < 140f) le.minHeight = 160f;
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        if (img.color.a < 0.05f) img.color = new Color(0.95f, 0.97f, 1f, 1f);
    }

    private void Toast(string msg)
    {
        Debug.Log("Inventory: " + msg, this);
    }
}