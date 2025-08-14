using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TankItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text info;
    [SerializeField] private Button rackBtn;

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

        string size = s.capacityL switch { 1000 => "Small", 3000 => "Med", 6000 => "Large", _ => $"{s.capacityL}L" };
        if (s.ferment == null)
        {
            title?.SetText($"{size} Tank ({s.capacityL} L) • Empty");
            info?.SetText("—");
            if (rackBtn) rackBtn.interactable = false;
            return;
        }

        var f = s.ferment;
        title?.SetText($"{size} Tank • {f.varietyName} {f.vintageYear}");
        info?.SetText(
            $"Liters: {f.liters}\n" +
            $"Ferment: {f.daysFermenting}/{f.targetDays} d\n" +
            $"Brix: {f.currentBrix:0.0} → ABV {f.alcoholABV:0.0}% (Yeast: {f.yeastName})"
        );

        if (rackBtn) rackBtn.interactable = ProductionSystem.I.CanRackToBarrel(s);
    }
}