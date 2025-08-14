using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class VineyardTileUI : MonoBehaviour
{
    [Header("UI refs")]
    [SerializeField] TMP_Text title;
    [SerializeField] Image readinessBar;          // child named "Readiness"
    [SerializeField] Button button;               // clickable root (or child)
    [SerializeField] Image bg;                    // background Image to tint

    [Header("Palette")]
    [SerializeField] Color lockedColor     = new Color(0.85f, 0.85f, 0.88f, 1f);
    [SerializeField] Color ownedEmptyColor = new Color(0.90f, 0.95f, 0.90f, 1f);
    [SerializeField] Color growingColor    = new Color(0.80f, 0.92f, 0.80f, 1f);
    [SerializeField] Color readyColor      = new Color(0.75f, 0.85f, 0.70f, 1f);

    [Header("Readiness")]
    [SerializeField] float harvestThreshold = 0.65f;

    public int TileIndex { get; private set; }

    // Grid can subscribe to either
    public event Action<VineyardTileUI> OnClicked;
    public event Action<int> OnClickedIndex;

    void Reset()        { AutoCache(); }
    void OnValidate()   { if (!Application.isPlaying) AutoCache(); }

    public void Bind(int index)
    {
        AutoCache();
        TileIndex = index;

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                OnClicked?.Invoke(this);
                OnClickedIndex?.Invoke(TileIndex);
            });
            // Always allow opening the detail (even when not owned)
            button.interactable = true;
        }

        RefreshImmediate();

        if (TimeController.I)
        {
            TimeController.I.OnNewDay -= RefreshImmediate;
            TimeController.I.OnNewDay += RefreshImmediate;
        }
    }

    void OnEnable()
    {
        AutoCache();
        if (TimeController.I) TimeController.I.OnNewDay += RefreshImmediate;
        RefreshImmediate();
    }
    void OnDisable()
    {
        if (TimeController.I) TimeController.I.OnNewDay -= RefreshImmediate;
    }

    void Update()
    {
        if (VineyardSystem.I == null) return;

        var s = VineyardSystem.I.GetState(TileIndex);
        if (s == null) return;

        float target = VineyardSystem.I.GetReadiness01(TileIndex);
        if (readinessBar)
        {
            readinessBar.fillAmount = Mathf.MoveTowards(readinessBar.fillAmount, target, Time.unscaledDeltaTime * 0.8f);
        }

        bool owned = s.owned;
        bool planted = !string.IsNullOrEmpty(s.plantedVariety);
        ApplyBackgroundTint(target, owned, planted);
    }

    public void RefreshImmediate()
    {
        if (VineyardSystem.I == null) return;
        var s = VineyardSystem.I.GetState(TileIndex);

        if (s == null)
        {
            if (title) title.text = $"Plot {TileIndex + 1}";
            if (readinessBar) readinessBar.gameObject.SetActive(false);
            ApplyBackgroundTint(0f, owned:false, planted:false);
            return;
        }

        bool owned   = s.owned;
        bool planted = !string.IsNullOrEmpty(s.plantedVariety);
        float r      = VineyardSystem.I.GetReadiness01(TileIndex);

        if (title)
        {
            if (!owned)          title.text = $"Plot {TileIndex + 1} (locked)";
            else if (!planted)   title.text = $"Plot {TileIndex + 1} (empty)";
            else                 title.text = $"{s.plantedVariety} • D{s.daysSincePlanting}";
        }

        if (readinessBar)
        {
            readinessBar.type = Image.Type.Filled;
            readinessBar.fillMethod = Image.FillMethod.Horizontal;
            readinessBar.fillOrigin = (int)Image.OriginHorizontal.Left;
            readinessBar.gameObject.SetActive(owned && planted);
            readinessBar.fillAmount = r;
            readinessBar.raycastTarget = false; // don't block clicks
        }

        if (title) title.raycastTarget = false; // don't block clicks

        // Keep button clickable for locked/owned/planted — detail panel will show Buy/Plant/Harvest as needed
        if (button) button.interactable = true;

        ApplyBackgroundTint(r, owned, planted);

        // Make sure BG doesn't eat clicks if it's on a child
        if (bg) bg.raycastTarget = false;
    }

    // alias for grid code
    public void Refresh() => RefreshImmediate();

    private void ApplyBackgroundTint(float readiness01, bool owned = true, bool planted = false)
    {
        if (!bg) return;

        if (!owned)     { bg.color = lockedColor; return; }
        if (!planted)   { bg.color = ownedEmptyColor; return; }

        Color baseCol = Color.Lerp(growingColor, readyColor, Mathf.SmoothStep(0f, 1f, readiness01));

        if (TimeController.I && GameConfigHolder.Instance)
        {
            int dpy = Mathf.Max(1, GameConfigHolder.Instance.Config.daysPerYear);
            float season01 = Mathf.Sin(((float)TimeController.I.DayOfYear / dpy * Mathf.PI * 2f) - Mathf.PI / 2f) * 0.5f + 0.5f;
            float shade = Mathf.Lerp(0.96f, 1.05f, season01);
            baseCol *= shade; baseCol.a = 1f;
        }

        bg.color = baseCol;
    }

    private void AutoCache()
    {
        if (!title)        title        = transform.Find("Title")?.GetComponent<TMP_Text>();
        if (!readinessBar) readinessBar = transform.Find("Readiness")?.GetComponent<Image>();
        if (!button)       button       = GetComponent<Button>();

        if (!bg)
        {
            // Prefer a child named "BG", else use root Image
            bg = transform.Find("BG")?.GetComponent<Image>();
            if (!bg) bg = GetComponent<Image>();
        }

        if (readinessBar)
        {
            readinessBar.type = Image.Type.Filled;
            readinessBar.fillMethod = Image.FillMethod.Horizontal;
            readinessBar.fillOrigin = (int)Image.OriginHorizontal.Left;
            readinessBar.raycastTarget = false;
        }
        if (title) title.raycastTarget = false;
        if (bg)    bg.raycastTarget = false;
    }
}
