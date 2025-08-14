using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryItemUI : MonoBehaviour
{
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text info;
    [SerializeField] Button sellBtn;

    public string Id { get; private set; }
    private BottleEntryState state;
    private InventoryPanel panel;

    public void Setup(BottleEntryState s, InventoryPanel panel)
    {
        this.panel = panel;
        this.state = s;

        Id = panel.GetProp(s, "id", Id);

        // Build display text via safe getters (reflection)
        string variety = panel.GetProp(s, "varietyName", "Wine");
        int vintage    = panel.GetProp(s, "vintageYear", 0);
        int bottles    = panel.GetProp(s, "bottles", 0);
        float quality  = panel.GetProp(s, "qualityScore", panel.GetProp(s, "quality", 0f));

        title?.SetText($"{variety} {vintage}");
        info?.SetText($"Bottles: {bottles}\nQuality: {quality:0.0}");

        if (sellBtn)
        {
            sellBtn.onClick.RemoveAllListeners();
            sellBtn.onClick.AddListener(() => panel.OpenSell(state));
            sellBtn.interactable = bottles > 0;
        }
    }

    // Optional convenience for builder wiring
    public void __EditorAssign(TMP_Text t, TMP_Text i, Button b)
    {
        title = t; info = i; sellBtn = b;
    }
}