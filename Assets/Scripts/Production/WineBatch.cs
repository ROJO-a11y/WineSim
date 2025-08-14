using System;

[Serializable]
public class WineBatch
{
    public string varietyName;
    public int vintageYear;
    public bool isRed;
    public float initialQuality; // 0..100 at bottling
    public int bottles;
    public int bottledDay;
}