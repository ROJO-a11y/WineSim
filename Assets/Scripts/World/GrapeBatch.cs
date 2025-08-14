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
}