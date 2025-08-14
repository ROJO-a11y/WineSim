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
    public bool BuyTankMed()   => ProductionSystem.I.TryBuyTank(3000);
    public bool BuyTankLarge() => ProductionSystem.I.TryBuyTank(6000);
    public bool BuyBarrel()    => ProductionSystem.I.TryBuyBarrel();
    public bool BuyBottler()   => ProductionSystem.I.TryBuyBottlingMachine();

    public bool RackToBarrel(string tankId) => ProductionSystem.I.RackToBarrel(tankId);
    public bool Bottle(string barrelId, out WineBatch bottled) => ProductionSystem.I.BottleBarrel(barrelId, out bottled);

    // Inventory actions
    public int SellBottles(string variety, int vintage, int count, out int pricePerBottle)
        => InventorySystem.I.Sell(variety, vintage, count, out pricePerBottle);
}
