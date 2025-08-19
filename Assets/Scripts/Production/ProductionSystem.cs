using UnityEngine;
using System.Collections.Generic;



public class ProductionSystem : MonoBehaviour
{

    public YeastSO[] Yeasts => yeasts;
    public static ProductionSystem I { get; private set; }
    [SerializeField] private GameConfig cfg;
    [SerializeField] private YeastSO[] yeasts;
    [SerializeField] private GrapeVarietySO[] varieties;

    private List<TankState> tanks = new();
    private List<BarrelState> barrels = new();
    public bool HasBottlingMachine { get; private set; }

    public IReadOnlyList<TankState> Tanks => tanks;
    public IReadOnlyList<BarrelState> Barrels => barrels;

    // --- Config helpers ---
    private GameConfig Conf() => GameConfigHolder.Instance ? GameConfigHolder.Instance.Config : cfg;
    private int BottleSizeMl()
    {
        var c = Conf();
        return c != null ? Mathf.Max(1, c.bottleSizeMl) : 750;
    }
    private float BarrelSweetBonus(int days)
    {
        var c = Conf();
        int min = c != null ? c.barrelSweetSpotDaysMin : 120;
        int max = c != null ? c.barrelSweetSpotDaysMax : 240;
        if (max <= min) return 1f;
        if (days < min)
        {
            float t = Mathf.Clamp01((min - days) / (float)(max - min));
            return Mathf.Lerp(1f, 0.5f, t);
        }
        if (days > max)
        {
            float t = Mathf.Clamp01((days - max) / (float)(max - min));
            return Mathf.Lerp(1f, 0.5f, t);
        }
        return 1f;
    }

    void Awake() { I = this; }

    void OnEnable()
    {
        if (TimeController.I != null) TimeController.I.OnNewDay += TickDaily;
    }

    void OnDisable()
    {
        if (TimeController.I != null) TimeController.I.OnNewDay -= TickDaily;
    }

    public void InitNewGame()
    {
        tanks.Clear(); barrels.Clear();
        HasBottlingMachine = false;
    }

    public void LoadFromSave(SaveData s)
    {
        tanks = new List<TankState>(s.tanks ?? new TankState[0]);
        barrels = new List<BarrelState>(s.barrels ?? new BarrelState[0]);
        HasBottlingMachine = s.hasBottlingMachine;
    }

    public TankState[] SerializeTanks() => tanks.ToArray();
    public BarrelState[] SerializeBarrels() => barrels.ToArray();

    // --- Placement / buying ---
    public bool TryBuyTank(int capacityL)
    {
        int price = capacityL switch { 1000 => cfg.smallTankPrice, 3000 => cfg.medTankPrice, 6000 => cfg.largeTankPrice, _ => cfg.smallTankPrice };
        if (!EconomySystem.I.TrySpend(price)) return false;
        tanks.Add(new TankState { id = System.Guid.NewGuid().ToString(), capacityL = capacityL, ferment = null });
        return true;
    }

    public bool TryBuyBarrel()
    {
        if (!EconomySystem.I.TrySpend(cfg.barrelPrice)) return false;
        barrels.Add(new BarrelState { id = System.Guid.NewGuid().ToString(), capacityL = cfg.barrelVolumeL, aging = null });
        return true;
    }

    public bool TryBuyBottlingMachine()
    {
        if (HasBottlingMachine) return false;
        if (!EconomySystem.I.TrySpend(cfg.bottlingMachinePrice)) return false;
        HasBottlingMachine = true; return true;
    }

    // --- Harvest ingress ---
    public bool TryReceiveHarvest(GrapeBatch batch, string yeastName)
    {
        int idx, cap, liters;
        return TryReceiveHarvestWithLog(batch, yeastName, out idx, out cap, out liters);
    }

    public bool TryReceiveHarvestWithLog(GrapeBatch batch, string yeastName, out int tankIndex, out int tankCapacity, out int liters)
    {
        liters = Mathf.RoundToInt(batch.kg * 0.7f);
        tankIndex = -1; tankCapacity = 0;

        int bestIdx = -1;
        int bestCap = int.MaxValue;
        for (int i = 0; i < tanks.Count; i++)
        {
            var t = tanks[i];
            if (t.ferment != null) continue;          // occupied
            if (liters > t.capacityL) continue;       // too small
            if (t.capacityL < bestCap) { bestCap = t.capacityL; bestIdx = i; }
        }

        if (bestIdx >= 0)
        {
            var t = tanks[bestIdx];
            StartFermentation(t, batch, yeastName, liters);
            tankIndex = bestIdx;
            tankCapacity = t.capacityL;
            return true;
        }
        return false;
    }

    private void StartFermentation(TankState tank, GrapeBatch batch, string yeastName, int liters)
    {
        var yeast = System.Array.Find(yeasts, y => y.yeastName == yeastName) ?? yeasts[0];
        int target = Mathf.RoundToInt(Mathf.Lerp(cfg.minFermentDays, cfg.maxFermentDays, 1f - Mathf.Clamp01(batch.brix / 26f)) / Mathf.Max(0.5f, yeast.speed));

        tank.ferment = new FermentState
        {
            varietyName = batch.varietyName,
            vintageYear = batch.vintageYear,
            startBrix = batch.brix,
            currentBrix = batch.brix,
            pH = batch.pH,
            phenolic = batch.phenolic,
            alcoholABV = 0f,
            yeastName = yeast.yeastName,
            daysFermenting = 0,
            targetDays = Mathf.Clamp(target, cfg.minFermentDays, cfg.maxFermentDays),
            liters = liters
        };
    }

    public void TickDaily()
    {
        // Fermentation
        foreach (var t in tanks)
        {
            if (t.ferment == null) continue;
            var f = t.ferment;
            f.daysFermenting++;

            // Linear-ish drop in Brix to near 0 by targetDays, plus tolerance check
            float step = f.startBrix / Mathf.Max(1, f.targetDays);
            f.currentBrix = Mathf.Max(0f, f.currentBrix - step);
            f.alcoholABV = Mathf.Min(f.startBrix * cfg.brixToAlcohol, (System.Array.Find(yeasts, y => y.yeastName == f.yeastName)?.toleranceABV) ?? 16f);

            // Stall if exceeding tolerance (keeps some residual sugar)
            if (f.alcoholABV >= (System.Array.Find(yeasts, y => y.yeastName == f.yeastName)?.toleranceABV))
                f.currentBrix = Mathf.Max(f.currentBrix, 2f);

            t.ferment = f;
        }

        // Barrel aging: accrue oak and craft quality, apply sweet-spot curve later when bottling
        foreach (var b in barrels)
        {
            if (b.aging == null) continue;
            b.aging.daysInBarrel++;
            var c = Conf();
            float oakPerDay = c != null ? c.barrelOakGainPerDay : cfg.barrelOakGainPerDay;
            b.aging.craftQuality += oakPerDay * BarrelSweetBonus(b.aging.daysInBarrel);
            if (c != null && (b.aging.daysInBarrel < c.barrelSweetSpotDaysMin || b.aging.daysInBarrel > c.barrelSweetSpotDaysMax))
            {
                b.aging.craftQuality -= c.overUnderPenaltyPerDay * 0.25f; // gentle drift down if outside window
            }
        }
    }

    public bool CanRackToBarrel(TankState sourceTank)
    {
        if (sourceTank?.ferment == null) return false;
        // consider fermentation done when days >= target OR brix <= 1.0
        return sourceTank.ferment.daysFermenting >= sourceTank.ferment.targetDays || sourceTank.ferment.currentBrix <= 1.0f;
    }

    public bool RackToBarrel(string tankId)
    {
        var tank = tanks.Find(t => t.id == tankId);
        if (tank == null || !CanRackToBarrel(tank)) return false;

        // liters to move from the tank (strict whole batch)
        int liters = tank.ferment.liters;

        // pick the smallest empty barrel that fits (capacityL >= liters)
        int bestIdx = -1;
        int bestCap = int.MaxValue;
        for (int i = 0; i < barrels.Count; i++)
        {
            var b = barrels[i];
            if (b == null) continue;
            if (b.aging != null) continue;    // occupied
            if (b.capacityL < liters) continue; // too small
            if (b.capacityL < bestCap) { bestCap = b.capacityL; bestIdx = i; }
        }

        if (bestIdx < 0) return false; // no empty barrel large enough

        var dest = barrels[bestIdx];

        // Preserve your craft baseline + yeast aroma contribution
        float fermentCraft = 10f;
        var yeast = System.Array.Find(yeasts, y => y.yeastName == tank.ferment.yeastName);
        if (yeast != null) fermentCraft += yeast.aroma;

        dest.aging = new AgingState
        {
            varietyName  = tank.ferment.varietyName,
            vintageYear  = tank.ferment.vintageYear,
            liters       = liters,
            daysInBarrel = 0,
            craftQuality = fermentCraft
        };

        // Clear the tank after moving the whole batch
        tank.ferment = null;
        return true;
    }

    public bool RackToBarrel(int tankIndex)
    {
        if (tankIndex < 0 || tankIndex >= tanks.Count) return false;
        return RackToBarrel(tanks[tankIndex].id);
    }

    public bool CanBottle(BarrelState barrel)
    {
        return HasBottlingMachine && barrel?.aging != null && barrel.aging.liters > 0;
    }

    public bool BottleBarrel(string barrelId, out WineBatch bottled)
    {
        bottled = null;
        var b = barrels.Find(x => x.id == barrelId);
        if (b == null || !CanBottle(b)) return false;

        // Compute quality with sweet spot window & penalties
        var variety = System.Array.Find(varieties, v => v.varietyName == b.aging.varietyName);
        int d = b.aging.daysInBarrel;
        float curve;
        var c = Conf();
        if (c != null)
        {
            int min = c.barrelSweetSpotDaysMin;
            int max = c.barrelSweetSpotDaysMax;
            if (d < min)      curve = 1f - (min - d) * (c.overUnderPenaltyPerDay / 100f);
            else if (d > max) curve = 1f - (d - max) * (c.overUnderPenaltyPerDay / 100f);
            else              curve = 1.1f; // bonus in window
        }
        else
        {
            if (d < variety.sweetSpotStart)      curve = 1f - (variety.sweetSpotStart - d) * (cfg.overUnderPenaltyPerDay / 100f);
            else if (d > variety.sweetSpotEnd)   curve = 1f - (d - variety.sweetSpotEnd) * (cfg.overUnderPenaltyPerDay / 100f);
            else                                  curve = 1.1f;
        }

        curve = Mathf.Clamp(curve, 0.6f, 1.2f);

        float quality = 50f + b.aging.craftQuality + variety.terroirBonus;
        quality *= curve;
        quality = Mathf.Clamp(quality, 0f, 100f);

        int ml = BottleSizeMl();
        int bottles = Mathf.FloorToInt((b.aging.liters * 1000f) / Mathf.Max(1, ml));

        bottled = new WineBatch
        {
            varietyName = variety.varietyName,
            vintageYear = b.aging.vintageYear,
            isRed = variety.isRed,
            initialQuality = quality,
            bottledDay = TimeController.I.Day,
            bottles = bottles
        };

        b.aging = null;

        // Add to inventory
        InventorySystem.I.AddBottles(bottled);
        return true;
    }
}