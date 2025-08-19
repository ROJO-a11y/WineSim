using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TankItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text info;
    [SerializeField] private Button rackBtn;

    // Config access helper
    private static GameConfig Cfg => GameConfigHolder.Instance ? GameConfigHolder.Instance.Config : null;

    // Map a capacity in liters to a friendly size label using GameConfig (falls back to literals)
    private string SizeLabelForCapacity(int cap)
    {
        if (Cfg != null)
        {
            int small = Cfg.smallTankCapacityL;
            int med   = Cfg.medTankCapacityL;
            int large = Cfg.largeTankCapacityL;
            if (cap == small) return "Small";
            if (cap == med)   return "Med";
            if (cap == large) return "Large";
            // closest match (rare but helpful if capacities were edited)
            int[] arr = { small, med, large };
            int pick = arr[0];
            int best = Mathf.Abs(cap - pick);
            for (int i = 1; i < arr.Length; i++)
            {
                int d = Mathf.Abs(cap - arr[i]);
                if (d < best) { best = d; pick = arr[i]; }
            }
            if (pick == small) return "Small~";
            if (pick == med)   return "Med~";
            if (pick == large) return "Large~";
        }
        // fallback to common literals
        switch (cap)
        {
            case 1000: return "Small";
            case 3000: return "Med";
            case 6000: return "Large";
            default:   return $"{cap:n0}L";
        }
    }

    public string Id { get; private set; }
    private FacilityPanel panel;

    public void Setup(TankState s, FacilityPanel panel)
    {
        this.panel = panel;
        Id = s.id;
        UpdateView(s);

        if (rackBtn)
        {
            rackBtn.onClick.RemoveAllListeners();
            rackBtn.onClick.AddListener(() => this.panel.RackTank(Id));
        }
    }

    public void UpdateView(TankState s)
    {
        if (s.id == null) { Destroy(gameObject); return; }

        string size = SizeLabelForCapacity(s.capacityL);
        if (s.ferment == null)
        {
            title?.SetText($"{size} Tank ({s.capacityL:n0} L) • Empty");
            info?.SetText("—");
            if (rackBtn) rackBtn.interactable = false;
            return;
        }

        var f = s.ferment;
        title?.SetText($"{size} Tank ({s.capacityL:n0} L) • {f.varietyName} {f.vintageYear}");
        var yeastLabel = string.IsNullOrEmpty(f.yeastName) ? "–" : f.yeastName;
        info?.SetText(
            $"Liters: {f.liters:n0}\n" +
            $"Ferment: {f.daysFermenting}/{f.targetDays} d\n" +
            $"Brix: {f.currentBrix:0.0} → ABV {f.alcoholABV:0.0}% (Yeast: {yeastLabel})"
        );

        if (rackBtn) rackBtn.interactable = (ProductionSystem.I && ProductionSystem.I.CanRackToBarrel(s));
    }
}