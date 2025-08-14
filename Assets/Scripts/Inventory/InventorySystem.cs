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
        e.bottles += batch.bottles;
        e.quality = (e.quality + batch.initialQuality) * 0.5f; // blend if multiple batches of same key
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

    public IReadOnlyCollection<BottleEntryState> AllStock() => stock.Values;
}