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
        Rebuild();
    }

    public void Rebuild()
    {
        if (stockContent == null || stockItemPrefab == null)
        {
            Debug.LogError("InventoryPanel: stockContent or stockItemPrefab not assigned.", this);
            return;
        }

        foreach (Transform c in stockContent) Destroy(c.gameObject);
        items.Clear();

        var entries = InventorySystem.I?.Serialize() ?? Array.Empty<BottleEntryState>();
        foreach (var e in entries)
        {
            var item = Instantiate(stockItemPrefab, stockContent);
            EnsureItemSizing(item.gameObject);
            item.Setup(e, this);
            items.Add(item);
        }
    }

    private void RefreshTop()
    {
        if (cashText) cashText.text = EconomySystem.I ? $"Cash: ${EconomySystem.I.Cash:n0}" : "Cash: ?";
    }

    // ---------- Sell Modal ----------

    public void OpenSell(BottleEntryState entry)
    {
        selected = entry;
        selectedId = GetProp(entry, "id", "");
        selectedMax = Mathf.Max(0, GetProp(entry, "bottles", 0));

        string variety = GetProp(entry, "varietyName", "Wine");
        int vintage = GetProp(entry, "vintageYear", 0);
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

                if (TrySell(selectedId, q, out int sold, out int revenue))
                {
                    Toast($"Sold {sold} for ${revenue:n0}");
                    CloseSell();
                    RefreshTop();
                    Rebuild();
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
        if (sellModal) sellModal.SetActive(true);
    }

    public void CloseSell()
    {
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

        var inv = InventorySystem.I;
        if (inv != null)
        {
            // Try common method signatures on InventorySystem via reflection
            var type = inv.GetType();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            // 1) bool TrySell(string id, int count, out int sold, out int revenue)
            var m = methods.FirstOrDefault(x =>
                x.Name.ToLower().Contains("sell") &&
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

            // 2) int Sell(string id, int count) -> returns revenue, sold assumed = count (if enough)
            m = methods.FirstOrDefault(x =>
                x.Name.ToLower().Contains("sell") &&
                x.ReturnType == typeof(int) &&
                MatchSig(x, new[] { typeof(string), typeof(int) })
            );
            if (m != null)
            {
                int rev = (int)m.Invoke(inv, new object[] { id, count });
                if (rev > 0) { revenue = rev; sold = count; return true; }
            }
        }

        // Try ActionAPI as a last resort: Sell*(string id, int count) -> bool
        var api = FindFirstObjectByType<ActionAPI>();
        if (api)
        {
            var typeA = api.GetType();
            var m = typeA.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => x.Name.ToLower().StartsWith("sell") && MatchSig(x, new[] { typeof(string), typeof(int) }));
            if (m != null)
            {
                bool ok = (bool)m.Invoke(api, new object[] { id, count });
                if (ok) { sold = count; revenue = Mathf.RoundToInt(count * unitPriceEst); }
                return ok;
            }
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
        var pi = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (pi != null && pi.FieldType == typeof(T)) return (T)pi.GetValue(obj);

        var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && typeof(T).IsAssignableFrom(prop.PropertyType))
            return (T)prop.GetValue(obj);

        // allow common numeric conversions
        if (prop != null && prop.PropertyType.IsValueType && typeof(T) == typeof(float))
        {
            try { return (T)Convert.ChangeType(prop.GetValue(obj), typeof(T)); } catch { }
        }
        if (prop != null && prop.PropertyType.IsValueType && typeof(T) == typeof(int))
        {
            try { return (T)Convert.ChangeType(prop.GetValue(obj), typeof(T)); } catch { }
        }

        return fallback;
    }

    private void AutoCache()
    {
        // Top
        cashText = cashText ? cashText : transform.Find("TopRow/CashText")?.GetComponent<TMP_Text>();

        // List
        var stockScroll = transform.Find("StockScroll");
        if (stockScroll)
        {
            var sr = stockScroll.GetComponent<ScrollRect>() ?? stockScroll.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;

            var vp = sr.viewport;
            if (!vp)
            {
                var vpt = stockScroll.Find("Viewport") as RectTransform;
                if (!vpt)
                {
                    var go = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
                    vpt = go.GetComponent<RectTransform>();
                    vpt.SetParent(stockScroll, false);
                }
                vpt.anchorMin = Vector2.zero; vpt.anchorMax = Vector2.one; vpt.offsetMin = Vector2.zero; vpt.offsetMax = Vector2.zero;
                sr.viewport = vpt;
            }
            if (!sr.content)
            {
                var ct = sr.viewport.Find("Content") as RectTransform;
                if (!ct)
                {
                    var go = new GameObject("Content", typeof(RectTransform));
                    ct = go.GetComponent<RectTransform>();
                    ct.SetParent(sr.viewport, false);
                }
                ct.anchorMin = Vector2.zero; ct.anchorMax = Vector2.one; ct.offsetMin = Vector2.zero; ct.offsetMax = Vector2.zero;
                var vlg = ct.GetComponent<VerticalLayoutGroup>() ?? ct.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 8; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
                var fit = ct.GetComponent<ContentSizeFitter>() ?? ct.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                sr.content = ct;
            }
            stockContent = sr.content;
            var le = stockScroll.GetComponent<LayoutElement>() ?? stockScroll.gameObject.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
        }

        // Sell modal
        sellModal     = sellModal     ? sellModal     : transform.Find("SellModal")?.gameObject;
        sellTitle     = sellTitle     ? sellTitle     : transform.Find("SellModal/Title")?.GetComponent<TMP_Text>();
        qtyInput      = qtyInput      ? qtyInput      : transform.Find("SellModal/QtyInput")?.GetComponent<TMP_InputField>();
        maxText       = maxText       ? maxText       : transform.Find("SellModal/MaxText")?.GetComponent<TMP_Text>();
        estText       = estText       ? estText       : transform.Find("SellModal/EstText")?.GetComponent<TMP_Text>();
        confirmSellBtn= confirmSellBtn? confirmSellBtn: transform.Find("SellModal/ConfirmBtn")?.GetComponent<Button>();
        cancelSellBtn = cancelSellBtn ? cancelSellBtn : transform.Find("SellModal/CancelBtn")?.GetComponent<Button>();

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