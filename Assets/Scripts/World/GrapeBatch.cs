using System;

[Serializable]
public class GrapeBatch
{
    public string varietyName;
    public int vintageYear;
    public float brix;
    public float pH;
    public float phenolic;
    public int kg; // weight harvested
    // Advanced composition at harvest
    public float TA;               // g/L titratable acidity
    public float YAN;              // mg N/L (yeast assimilable nitrogen)
    public float waterStress01;    // 0..1 plant stress (from VPD + low soil moisture)
    public float diseasePressure01;// 0..1 mildew/rot pressure accumulator
    public float colorIndex;       // 0..100 color/anthocyanin proxy
    public float aromaIndex;       // 0..100 aroma precursor proxy
    public float grapeTempC;       // °C of must at crush/handoff
    public float dilution01;       // 0..1 hydration/dilution proxy (rain near harvest, high soil moisture)
}