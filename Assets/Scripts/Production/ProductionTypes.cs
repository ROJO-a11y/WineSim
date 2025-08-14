using System;

[Serializable]
public class TankState
{
    public string id;
    public int capacityL;         // 1000 / 3000 / 6000
    public FermentState ferment;  // null if empty
}

[Serializable]
public class FermentState
{
    public string varietyName;
    public int vintageYear;
    public float startBrix;
    public float currentBrix;
    public float pH;
    public float phenolic;
    public float alcoholABV;
    public string yeastName;
    public int daysFermenting;
    public int targetDays; // computed from yeast & brix
    public int liters;     // must not exceed tank capacity
}

[Serializable]
public class BarrelState
{
    public string id;
    public int capacityL; // 225
    public AgingState aging; // null if empty
}

[Serializable]
public class AgingState
{
    public string varietyName;
    public int vintageYear;
    public int liters;
    public int daysInBarrel;
    public float craftQuality; // includes ferment + oak
}