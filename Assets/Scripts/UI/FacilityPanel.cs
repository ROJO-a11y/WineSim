using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

[DefaultExecutionOrder(500)]
public class FacilityPanel : MonoBehaviour
{
    [Header("Top info")]
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private TMP_Text bottlerText;

    [Header("Buy buttons (auto-wired by name)")]
    [SerializeField] private Button buyTankSmallBtn;
    [SerializeField] private Button buyTankMedBtn;
    [SerializeField] private Button buyTankLargeBtn;
    [SerializeField] private Button buyBarrelBtn;
    [SerializeField] private Button buyBottlerBtn;

    [Header("Lists (auto-wired)")]
    [SerializeField] private RectTransform tanksContent;   // will be ScrollRect.content
    [SerializeField] private RectTransform barrelsContent; // will be ScrollRect.content
    [SerializeField] private TankItemUI tankItemPrefab;
    [SerializeField] private BarrelItemUI barrelItemPrefab;

    [Header("Feedback (optional)")]
    [SerializeField] private TMP_Text toastText;

    private readonly List<TankItemUI> tankItems = new();
    private readonly List<BarrelItemUI> barrelItems = new();

    void Reset()      { AutoCache(); }
    void OnValidate() { if (!Application.isPlaying) AutoCache(); }

    void OnEnable()
    {
        AutoCache();
        WireBuyButtons();
        StartCoroutine(InitAndBuild());
        if (TimeController.I) TimeController.I.OnNewDay += RefreshAll;
    }

    void OnDisable()
    {
        if (TimeController.I) TimeController.I.OnNewDay -= RefreshAll;
    }

    IEnumerator InitAndBuild()
    {
        // Wait for core systems to be ready
        int frames = 0;
        while (ProductionSystem.I == null && frames < 120) { frames++; yield return null; }

        if (!Validate(out string reason))
        {
            Debug.LogError($"FacilityPanel: Not ready → {reason}", this);
            yield break;
        }

        Debug.Log($"FacilityPanel wired:\n - tanksContent = {PathOf(tanksContent)}\n - barrelsContent = {PathOf(barrelsContent)}", this);

        RebuildAll();
        RefreshTop();
    }

    private bool Validate(out string reason)
    {
        if (ProductionSystem.I == null) { reason = "ProductionSystem.I is null"; return false; }

        // Try to self-wire once more if anything is missing
        if (!tanksContent || !barrelsContent || !tankItemPrefab || !barrelItemPrefab) AutoCache();

        if (!tanksContent)       { reason = "tanksContent not assigned (couldn't find TanksScroll/Viewport/Content)"; return false; }
        if (!barrelsContent)     { reason = "barrelsContent not assigned (couldn't find BarrelsScroll/Viewport/Content)"; return false; }
        if (!tankItemPrefab)     { reason = "tankItemPrefab not assigned (assign your TankItemPrefab)"; return false; }
        if (!barrelItemPrefab)   { reason = "barrelItemPrefab not assigned (assign your BarrelItemPrefab)"; return false; }

        reason = null; return true;
    }

    private void WireBuyButtons()
    {
        // Clear & rebind each time we enable
        buyTankSmallBtn?.onClick.RemoveAllListeners();
        buyTankMedBtn  ?.onClick.RemoveAllListeners();
        buyTankLargeBtn?.onClick.RemoveAllListeners();
        buyBarrelBtn   ?.onClick.RemoveAllListeners();
        buyBottlerBtn  ?.onClick.RemoveAllListeners();

        if (ProductionSystem.I == null) return;

        buyTankSmallBtn?.onClick.AddListener(() => { if (ProductionSystem.I.TryBuyTank(1000)) RebuildAllWithToast("Bought 1kL tank"); });
        buyTankMedBtn  ?.onClick.AddListener(() => { if (ProductionSystem.I.TryBuyTank(3000)) RebuildAllWithToast("Bought 3kL tank"); });
        buyTankLargeBtn?.onClick.AddListener(() => { if (ProductionSystem.I.TryBuyTank(6000)) RebuildAllWithToast("Bought 6kL tank"); });
        buyBarrelBtn   ?.onClick.AddListener(() => { if (ProductionSystem.I.TryBuyBarrel()) RebuildAllWithToast("Bought barrel"); });
        buyBottlerBtn  ?.onClick.AddListener(() =>
        {
            if (ProductionSystem.I.TryBuyBottlingMachine())
            {
                RefreshTop();
                Toast("Bought bottling machine");
            }
        });
    }

    // Called by TankItemUI
    public void RackTank(string tankId)
    {
        if (ProductionSystem.I != null && ProductionSystem.I.RackToBarrel(tankId))
            RebuildAllWithToast("Racked to barrel");
        else
            Toast("No empty barrel (or batch not ready).");
    }

    // Optional overload: allow calling by tank index
    public void RackTank(int tankIndex)
    {
        var ps = ProductionSystem.I;
        if (ps == null)
        {
            Toast("No ProductionSystem.");
            return;
        }
        var tanks = ps.SerializeTanks() ?? new TankState[0];
        if (tankIndex < 0 || tankIndex >= tanks.Length)
        {
            Toast("Invalid tank index.");
            return;
        }
        var tid = tanks[tankIndex].id;
        RackTank(tid);
    }

    // Called by BarrelItemUI
    public void BottleBarrel(string barrelId)
    {
        if (ProductionSystem.I != null && ProductionSystem.I.BottleBarrel(barrelId, out var bottled))
            RebuildAllWithToast($"Bottled {bottled.bottles}x {bottled.varietyName} {bottled.vintageYear}");
        else
            Toast("Need bottling machine and an eligible barrel.");
    }

    // Optional overload: allow calling by barrel index
    public void BottleBarrel(int barrelIndex)
    {
        var ps = ProductionSystem.I;
        if (ps == null)
        {
            Toast("No ProductionSystem.");
            return;
        }
        var barrels = ps.SerializeBarrels() ?? new BarrelState[0];
        if (barrelIndex < 0 || barrelIndex >= barrels.Length)
        {
            Toast("Invalid barrel index.");
            return;
        }
        var bid = barrels[barrelIndex].id;
        BottleBarrel(bid);
    }

    private void RebuildAllWithToast(string msg) { RebuildAll(); Toast(msg); }
    private void Toast(string msg) { if (!toastText) return; toastText.text = msg; CancelInvoke(nameof(ClearToast)); Invoke(nameof(ClearToast), 2f); }
    private void ClearToast() { if (toastText) toastText.text = ""; }

    private void RebuildAll()
    {
        if (!Validate(out var reason)) { Debug.LogError($"FacilityPanel.RebuildAll() aborted → {reason}", this); return; }

        // Clear
        foreach (Transform c in tanksContent) Destroy(c.gameObject);
        foreach (Transform c in barrelsContent) Destroy(c.gameObject);
        tankItems.Clear(); barrelItems.Clear();

        // Tanks
        var tanks = ProductionSystem.I.SerializeTanks() ?? new TankState[0];
        foreach (var t in tanks)
        {
            var item = Instantiate(tankItemPrefab, tanksContent);
            EnsureItemVisual(item, false);
            item.Setup(t, this);
            tankItems.Add(item);
        }

        // Barrels
        var barrels = ProductionSystem.I.SerializeBarrels() ?? new BarrelState[0];
        foreach (var b in barrels)
        {
            var item = Instantiate(barrelItemPrefab, barrelsContent);
            EnsureItemVisual(item, true);
            item.Setup(b, this);
            barrelItems.Add(item);
        }

        RefreshAll();
    }

    private void RefreshAll()
    {
        if (ProductionSystem.I == null) return;

        var tankSnap = (ProductionSystem.I.SerializeTanks() ?? new TankState[0]).ToDictionary(x => x.id, x => x);
        foreach (var item in tankItems) { tankSnap.TryGetValue(item.Id, out var s); item.UpdateView(s); }

        var barrelSnap = (ProductionSystem.I.SerializeBarrels() ?? new BarrelState[0]).ToDictionary(x => x.id, x => x);
        foreach (var item in barrelItems) { barrelSnap.TryGetValue(item.Id, out var s); item.UpdateView(s); }

        RefreshTop();
    }

    private void RefreshTop()
    {
        if (cashText)    cashText.text = EconomySystem.I != null ? $"Cash: ${EconomySystem.I.Cash:n0}" : "Cash: ?";
        if (bottlerText) bottlerText.text = ProductionSystem.I != null && ProductionSystem.I.HasBottlingMachine ? "Bottler: Yes" : "Bottler: No";
    }

    // ---------- Auto-wiring helpers ----------

    private void AutoCache()
    {
        EnsureContainerLayout();

        // Top texts
        cashText    = cashText    ? cashText    : transform.Find("TopRow/CashText")?.GetComponent<TMP_Text>();
        bottlerText = bottlerText ? bottlerText : transform.Find("TopRow/BottlerText")?.GetComponent<TMP_Text>();

        // Buy buttons under TopRow/BuyRow
        var buyRow = transform.Find("TopRow/BuyRow");
        if (buyRow)
        {
            buyTankSmallBtn = buyTankSmallBtn ? buyTankSmallBtn : buyRow.Find("+1kLBtn")    ?.GetComponent<Button>();
            buyTankMedBtn   = buyTankMedBtn   ? buyTankMedBtn   : buyRow.Find("+3kLBtn")    ?.GetComponent<Button>();
            buyTankLargeBtn = buyTankLargeBtn ? buyTankLargeBtn : buyRow.Find("+6kLBtn")    ?.GetComponent<Button>();
            buyBarrelBtn    = buyBarrelBtn    ? buyBarrelBtn    : buyRow.Find("+BarrelBtn") ?.GetComponent<Button>();
            buyBottlerBtn   = buyBottlerBtn   ? buyBottlerBtn   : buyRow.Find("+BottlerBtn")?.GetComponent<Button>();
        }

        // Scrolls
        tanksContent   = EnsureScroll("TanksScroll", tanksContent);
        barrelsContent = EnsureScroll("BarrelsScroll", barrelsContent);
    }

    private void EnsureContainerLayout()
    {
        // Ensure the FacilityScreen (this GO) stacks children vertically
        var vlg = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.spacing = 12f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // TopRow gets a fixed/preferred height so scrolls get the rest
        var topRow = transform.Find("TopRow") as RectTransform;
        if (topRow)
        {
            var leTop = topRow.GetComponent<LayoutElement>() ?? topRow.gameObject.AddComponent<LayoutElement>();
            leTop.minHeight = 100f;
            leTop.preferredHeight = 120f;
            leTop.flexibleHeight = 0f;
        }

        // Scrolls share remaining height
        var tScroll = transform.Find("TanksScroll") as RectTransform;
        var bScroll = transform.Find("BarrelsScroll") as RectTransform;
        if (tScroll)
        {
            var leT = tScroll.GetComponent<LayoutElement>() ?? tScroll.gameObject.AddComponent<LayoutElement>();
            leT.flexibleHeight = 1f;
            leT.minHeight = 100f;
        }
        if (bScroll)
        {
            var leB = bScroll.GetComponent<LayoutElement>() ?? bScroll.gameObject.AddComponent<LayoutElement>();
            leB.flexibleHeight = 1f;
            leB.minHeight = 100f;
        }
    }

    private RectTransform EnsureScroll(string scrollName, RectTransform existing)
    {
        if (existing) return existing;

        var root = transform.Find(scrollName) as RectTransform;
        if (!root) return null;

        var sr = root.GetComponent<ScrollRect>();
        if (!sr) sr = root.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;

        // Viewport
        var vp = sr.viewport;
        if (!vp)
        {
            var vpT = root.Find("Viewport") as RectTransform;
            if (!vpT)
            {
                var vpGO = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
                vpT = vpGO.GetComponent<RectTransform>();
                vpT.SetParent(root, false);
            }
            vp = vpT;
            var img = vp.GetComponent<Image>(); img.color = new Color(1,1,1,0);
            var mask = vp.GetComponent<Mask>(); mask.showMaskGraphic = false;
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.offsetMin = Vector2.zero; vp.offsetMax = Vector2.zero;
            sr.viewport = vp;
        }

        // Content
        var content = sr.content;
        if (!content)
        {
            var cT = vp.Find("Content") as RectTransform;
            if (!cT)
            {
                var cGO = new GameObject("Content", typeof(RectTransform));
                cT = cGO.GetComponent<RectTransform>();
                cT.SetParent(vp, false);
            }
            cT.anchorMin = Vector2.zero; cT.anchorMax = Vector2.one; cT.offsetMin = Vector2.zero; cT.offsetMax = Vector2.zero;
            var vlg = cT.GetComponent<VerticalLayoutGroup>() ?? cT.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = cT.GetComponent<ContentSizeFitter>() ?? cT.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = cT;
            content = cT;
        }

        // Give the scroll some height in a VLG parent
        var le = root.GetComponent<LayoutElement>() ?? root.gameObject.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;

        // Optional: make the scroll root lightly visible (helps detect it's there)
        var bg = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
        if (bg.color.a < 0.01f) bg.color = new Color(0.96f, 0.96f, 0.96f, 1f);

        return content;
    }

    private void EnsureItemVisual(Component comp, bool isBarrel)
    {
        var go = comp.gameObject;
        var rt = go.GetComponent<RectTransform>();
        if (rt)
        {
            if (rt.sizeDelta.y < 100f) rt.sizeDelta = new Vector2(rt.sizeDelta.x, 140f);
            rt.localScale = Vector3.one;
        }
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (le.minHeight < 100f) le.minHeight = 140f;
        le.flexibleHeight = 0f;

        // Make sure it has a visible background
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        if (img.color.a < 0.05f)
        {
            img.color = isBarrel ? new Color(0.98f, 0.96f, 0.92f, 1f) : new Color(0.93f, 0.97f, 1f, 1f);
        }
    }

    private static string PathOf(Component c) => c ? PathOf(c.transform) : "(null)";
    private static string PathOf(Transform t)
    {
        if (!t) return "(null)";
        var p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
