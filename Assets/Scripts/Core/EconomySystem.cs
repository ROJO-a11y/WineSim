using UnityEngine;

public class EconomySystem : MonoBehaviour
{
    public static EconomySystem I { get; private set; }
    [SerializeField] private GameConfig cfg;

    public int Cash { get; private set; }
    public float MarketIndex { get; private set; }
    public float BrandLevel { get; private set; } // 1.0 baseline; climbs with good reviews

    private System.Random rng;

    void Awake() { I = this; rng = new System.Random(); }

    public void InitNewGame()
    {
        Cash = cfg.startingCash;
        MarketIndex = Mathf.Lerp(cfg.marketIndexMin, cfg.marketIndexMax, 0.5f);
        BrandLevel = 1f;
    }

    public void LoadFromSave(SaveData s)
    {
        Cash = s.cash; MarketIndex = s.marketIndex; BrandLevel = s.brandLevel;
    }

    public void TickDaily()
    {
        // random walk within bounds
        float delta = (float)(rng.NextDouble() * 2 - 1) * cfg.marketStepMax;
        MarketIndex = Mathf.Clamp(MarketIndex + delta, cfg.marketIndexMin, cfg.marketIndexMax);
    }

    public bool TrySpend(int amount)
    {
        if (Cash < amount) return false;
        Cash -= amount; return true;
    }

    public void Earn(int amount) => Cash += amount;

    public int PriceForWine(float quality, string variety, bool isRed)
    {
        // arcade price curve: base by quality (0..100) → euros per bottle
        float basePrice = Mathf.Lerp(5f, 120f, Mathf.Clamp01(quality / 100f));
        float brandBoost = Mathf.Lerp(0.9f, 1.3f, Mathf.Clamp01(BrandLevel - 0.5f)); // brand ~0.5..2.0
        float varietyMult = (variety == "Pinot Noir" || variety == "Cabernet Sauvignon") ? 1.1f : 1f;
        float styleMult = isRed ? 1.05f : 1f;
        float price = basePrice * MarketIndex * brandBoost * varietyMult * styleMult;
        return Mathf.RoundToInt(price);
    }

    public void BumpBrandFromReview(int reviewScore)
    {
        // small permanent brand nudge
        BrandLevel += Mathf.Clamp((reviewScore - 70) / 1000f, -0.02f, 0.06f);
        BrandLevel = Mathf.Clamp(BrandLevel, 0.5f, 2.0f);
    }
}