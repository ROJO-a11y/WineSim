using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class BottleEntryState
{
    public string varietyName;
    public int vintageYear;
    public bool isRed;
    public int bottles;
    public float quality;  // evolves in bottle
    public bool hasReview;
    public int reviewScore;
    public int bottledDay;
}

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem I { get; private set; }
    [SerializeField] private GameConfig cfg;
    [SerializeField] private GrapeVarietySO[] varieties;

    private Dictionary<string, BottleEntryState> stock = new();

    void Awake() { I = this; }

    public void InitNewGame() { stock.Clear(); }
    public void LoadFromSave(SaveData s)
    {
        stock.Clear();
        if (s.inventory != null)
            foreach (var e in s.inventory) stock[Key(e.varietyName, e.vintageYear)] = e;
    }

    public BottleEntryState[] Serialize()
    {
        var list = new List<BottleEntryState>(stock.Values);
        return list.ToArray();
    }

    private string Key(string v, int y) => $"{v}_{y}";

    public void AddBottles(WineBatch batch)
    {
        if (batch == null || string.IsNullOrEmpty(batch.varietyName) || batch.bottles <= 0) return;

        string k = Key(batch.varietyName, batch.vintageYear);
        if (!stock.TryGetValue(k, out var e))
        {
            e = new BottleEntryState
            {
                varietyName = batch.varietyName,
                vintageYear = batch.vintageYear,
                isRed = batch.isRed,
                bottles = 0,
                quality = batch.initialQuality,
                hasReview = false,
                reviewScore = 0,
                bottledDay = batch.bottledDay
            };
            stock[k] = e;
        }

        // Weighted-average quality by bottle count
        int newTotal = e.bottles + batch.bottles;
        if (newTotal <= 0) newTotal = 1;
        e.quality = Mathf.Clamp(
            (e.quality * e.bottles + batch.initialQuality * batch.bottles) / newTotal,
            0f, 100f
        );
        e.bottles = newTotal;
    }

    public void TickDaily()
    {
        foreach (var kv in stock)
        {
            var e = kv.Value;
            var variety = System.Array.Find(varieties, v => v.varietyName == e.varietyName);
            int age = TimeController.I.Day - e.bottledDay;

            if (e.isRed)
            {
                // reds improve up to N days then plateau
                if (age < variety.bottleImproveDays) e.quality += cfg.bottleImprovementPerDay;
            }
            else
            {
                // whites degrade slowly after threshold
                if (age > variety.bottleWhiteDegradeStart) e.quality -= cfg.bottleWhiteDegradePerDay;
            }

            e.quality = Mathf.Clamp(e.quality, 0f, 100f);
        }
    }

    public int Sell(string variety, int vintage, int count, out int pricePerBottle)
    {
        string k = Key(variety, vintage);
        pricePerBottle = 0;
        if (!stock.TryGetValue(k, out var e)) return 0;
        int sell = Mathf.Clamp(count, 0, e.bottles);
        if (sell <= 0) return 0;

        // First sale triggers "review"
        if (!e.hasReview)
        {
            e.reviewScore = Mathf.RoundToInt(Mathf.Lerp(60f, 95f, Mathf.Clamp01(e.quality / 100f)));
            e.hasReview = true;
            EconomySystem.I.BumpBrandFromReview(e.reviewScore);
        }

        pricePerBottle = EconomySystem.I.PriceForWine(e.quality, variety, e.isRed);
        int revenue = sell * pricePerBottle;
        e.bottles -= sell;

        if (e.bottles <= 0) stock.Remove(k);
        EconomySystem.I.Earn(revenue);
        return revenue;
    }

    // InventoryPanel expects a TrySell signature; provide it and route to Sell()
    public bool TrySell(string variety, int vintage, int qty, out int sold, out int revenue)
    {
        sold = 0; revenue = 0;
        if (string.IsNullOrEmpty(variety) || qty <= 0) return false;

        string k = Key(variety, vintage);
        if (!stock.TryGetValue(k, out var e) || e.bottles <= 0) return false;

        sold = Mathf.Min(qty, e.bottles);
        if (sold <= 0) return false;

        int pricePerBottle;
        revenue = Sell(variety, vintage, sold, out pricePerBottle);
        return revenue > 0;
    }

    // Alternate name some UIs search for; just forwards to TrySell
    public bool SellWine(string variety, int vintage, int qty, out int sold, out int revenue)
    {
        return TrySell(variety, vintage, qty, out sold, out revenue);
    }

    // Convenience for UI to show available quantity
    public int GetCount(string variety, int vintage)
    {
        string k = Key(variety, vintage);
        return stock.TryGetValue(k, out var e) ? e.bottles : 0;
    }


// ========= ID-based selling for InventoryPanel =========
// Expected key format: the same `id` used to build the inventory list entries.
// In our case, that's the dictionary key we already use internally: "Variety|Vintage".

public bool TrySell(string id, int count, out int sold, out int revenue)
{
    sold = 0; revenue = 0;
    if (string.IsNullOrEmpty(id) || count <= 0) return false;

    if (!stock.TryGetValue(id, out var e) || e.bottles <= 0) return false;

    sold = Mathf.Min(count, e.bottles);
    if (sold <= 0) return false;

    // Re-use your existing Sell(variety, vintage, count, out pricePerBottle)
    int pricePerBottle;
    revenue = Sell(e.varietyName, e.vintageYear, sold, out pricePerBottle);

    // If empty, remove entry
    if (stock.TryGetValue(id, out var e2) && e2.bottles <= 0)
        stock.Remove(id);

    return revenue > 0;
}

// Alternate signature: return total revenue (InventoryPanel will assume sold=count if rev>0)
public int Sell(string id, int count)
{
    if (string.IsNullOrEmpty(id) || count <= 0) return 0;
    if (!stock.TryGetValue(id, out var e) || e.bottles <= 0) return 0;

    int pricePerBottle;
    return Sell(e.varietyName, e.vintageYear, count, out pricePerBottle);
}
    public IReadOnlyCollection<BottleEntryState> AllStock() => stock.Values;
}