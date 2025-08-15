using UnityEngine;

public class ActionAPI : MonoBehaviour
{
    // Access shared config via the scene's GameConfigHolder
    private GameConfig Cfg => GameConfigHolder.Instance != null ? GameConfigHolder.Instance.Config : null;

    // Vineyard tile actions
    public bool TryBuyPlot(int idx) => VineyardSystem.I.TryBuyPlot(idx);
    public bool TryPlant(int idx, string varietyName) => VineyardSystem.I.TryPlant(idx, varietyName);

    // Harvest: if no empty tank to receive, auto-sell grapes to wholesale
    public void Harvest(int idx, string yeastName)
    {
        if (!VineyardSystem.I.CanHarvest(idx, out _)) return;

        var batch = VineyardSystem.I.Harvest(idx);
        bool accepted = ProductionSystem.I.TryReceiveHarvest(batch, yeastName);
        if (!accepted)
        {
            // Auto-sell to wholesale using shared config
            float pricePerKg = Cfg != null ? Cfg.grapeWholesalePerKg : 1f;
            int revenue = Mathf.RoundToInt(batch.kg * pricePerKg);
            EconomySystem.I.Earn(revenue);
        }
    }

    // Facility actions
    public bool BuyTankSmall() => ProductionSystem.I.TryBuyTank(1000);
    public bool BuyTankMed() => ProductionSystem.I.TryBuyTank(3000);
    public bool BuyTankLarge() => ProductionSystem.I.TryBuyTank(6000);
    public bool BuyBarrel() => ProductionSystem.I.TryBuyBarrel();
    public bool BuyBottler() => ProductionSystem.I.TryBuyBottlingMachine();

    public bool RackToBarrel(string tankId) => ProductionSystem.I.RackToBarrel(tankId);
    public bool Bottle(string barrelId, out WineBatch bottled) => ProductionSystem.I.BottleBarrel(barrelId, out bottled);

    // Inventory actions (existing revenue-by-price-per-bottle API)
    // Returns total revenue; pricePerBottle (out) is provided by InventorySystem.
    public int SellBottles(string variety, int vintage, int count, out int pricePerBottle)
        => InventorySystem.I.Sell(variety, vintage, count, out pricePerBottle);

    // ========= NEW: reflection-friendly wrappers for InventoryPanel =========
    // Most UIs search for one of these names/signatures. All route to InventorySystem.I.TrySell(...)

    /// <summary>
    /// Primary wrapper: matches common reflection probe
    /// TrySell(string variety, int vintage, int qty, out int sold, out int revenue)
    /// </summary>
    public bool TrySell(string variety, int vintage, int qty, out int sold, out int revenue)
    {
        sold = 0; revenue = 0;
        if (InventorySystem.I == null) return false;
        return InventorySystem.I.TrySell(variety, vintage, qty, out sold, out revenue);
    }

    /// <summary>
    /// Alternate name some panels look for.
    /// </summary>
    public bool SellWine(string variety, int vintage, int qty, out int sold, out int revenue)
    {
        return TrySell(variety, vintage, qty, out sold, out revenue);
    }

    /// <summary>
    /// Overload named "SellBottles" but returning sold+revenue (not pricePerBottle).
    /// Keeps the old SellBottles(int, out pricePerBottle) above intact.
    /// </summary>
    public bool SellBottles(string variety, int vintage, int qty, out int sold, out int revenue)
    {
        return TrySell(variety, vintage, qty, out sold, out revenue);
    }

    /// <summary>
    /// Generic "Sell" wrapper returning sold+revenue.
    /// </summary>
    public bool Sell(string variety, int vintage, int qty, out int sold, out int revenue)
    {
        return TrySell(variety, vintage, qty, out sold, out revenue);
    }

    /// <summary>
    /// Variant returning sold count and revenue via out (some panels expect return=int sold).
    /// </summary>
    public int Sell(string variety, int vintage, int qty, out int revenue)
    {
        int sold; revenue = 0;
        if (!TrySell(variety, vintage, qty, out sold, out revenue)) return 0;
        return sold;
    }
    
    // Reflection-friendly ActionAPI wrapper for InventoryPanel
    // Signature: Sell*(string id, int count) -> bool
    public bool SellById(string id, int count)
    {
        if (InventorySystem.I == null) return false;
        return InventorySystem.I.TrySell(id, count, out _, out _);
    }

    // Alias that also matches StartsWith("sell") and exact signature the panel probes
    public bool Sell(string id, int count)
    {
        if (InventorySystem.I == null) return false;
        return InventorySystem.I.TrySell(id, count, out _, out _);
    }

}