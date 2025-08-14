using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BarrelItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text info;
    [SerializeField] private Button bottleBtn;

    public string Id { get; private set; }
    private FacilityPanel panel;

    private int GetBarrelCapacityLiters(BarrelState s)
    {
        // 1) Prefer capacity on the BarrelState instance
        int cap = TryReadInt(s, new[] { "capacityL", "capacityLiters", "capacity" });
        if (cap > 0) return cap;

        // 2) Fallback to GameConfig (various common field/property names)
        var holder = GameConfigHolder.Instance;
        var cfg = holder != null ? holder.Config : null;
        if (cfg != null)
        {
            cap = TryReadInt(cfg, new[] {
                "barrelCapacityLiters","BarrelCapacityLiters",
                "barrelCapacityL","BarrelCapacityL",
                "barrelCapacity","BarrelCapacity",
                "defaultBarrelCapacity","DefaultBarrelCapacityL",
                "agingBarrelCapacity"
            });
            if (cap > 0) return cap;
        }

        // 3) Safe default
        return 225;
    }

    private int TryReadInt(object obj, string[] names)
    {
        if (obj == null) return -1;
        var t = obj.GetType();
        foreach (var n in names)
        {
            var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
            {
                try { return Convert.ToInt32(f.GetValue(obj)); } catch { }
            }
            var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanRead)
            {
                try { return Convert.ToInt32(p.GetValue(obj, null)); } catch { }
            }
        }
        return -1;
    }

    public void Setup(BarrelState s, FacilityPanel panel)
    {
        this.panel = panel;
        Id = s.id;
        UpdateView(s);

        if (bottleBtn)
        {
            bottleBtn.onClick.RemoveAllListeners();
            bottleBtn.onClick.AddListener(() => this.panel.BottleBarrel(Id));
        }
    }

    public void UpdateView(BarrelState s)
    {
        int cap = GetBarrelCapacityLiters(s);
        if (s.id == null) { Destroy(gameObject); return; }

        if (s.aging == null)
        {
            title?.SetText($"Barrel ({cap} L) • Empty");
            info?.SetText("—");
            if (bottleBtn) bottleBtn.interactable = false;
            return;
        }

        var a = s.aging;
        title?.SetText($"Barrel ({cap} L) • {a.varietyName} {a.vintageYear} • {a.liters} L");
        info?.SetText(
            $"Days in barrel: {a.daysInBarrel}\n" +
            $"Craft: {a.craftQuality:0.0}"
        );

        if (bottleBtn) bottleBtn.interactable = ProductionSystem.I.CanBottle(s);
    }
}