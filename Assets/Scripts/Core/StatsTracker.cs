using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight daily stats logger. Subscribes to TimeController.OnNewDay and
/// records cash, proxy revenue/expenses, and total bottles in inventory.
/// Lives across scene reloads if you want (optional).
/// </summary>
[DefaultExecutionOrder(-50)] // after core systems but before most UI
public class StatsTracker : MonoBehaviour
{
    public static StatsTracker I { get; private set; }

    [Header("History (most recent last)")]
    public List<int> revenueDaily = new();   // proxy: positive cash delta
    public List<int> expensesDaily = new();  // proxy: negative cash delta
    public List<int> bottlesDaily = new();
    public List<int> cashDaily = new();

    private int _prevCash;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        // Optional: keep it alive (uncomment if multi-scene)
        // DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        // Seed previous cash
        _prevCash = EconomySystem.I ? EconomySystem.I.Cash : 0;

        if (TimeController.I != null)
            TimeController.I.OnNewDay += SnapshotToday;

        // Log a first snapshot at enable so charts have one point
        SnapshotToday();
    }

    void OnDisable()
    {
        if (TimeController.I != null)
            TimeController.I.OnNewDay -= SnapshotToday;
    }

    private void SnapshotToday()
    {
        int cash = EconomySystem.I ? EconomySystem.I.Cash : 0;

        // Proxy revenue/expenses by cash delta (simple, good enough for v1)
        int delta = cash - _prevCash;
        int revenue = Mathf.Max(0, delta);
        int expense = Mathf.Max(0, -delta);

        int bottles = 0;
        var inv = InventorySystem.I;
        if (inv != null)
        {
            var arr = inv.Serialize() ?? System.Array.Empty<BottleEntryState>();
            for (int i = 0; i < arr.Length; i++) bottles += Mathf.Max(0, arr[i].bottles);
        }

        revenueDaily.Add(revenue);
        expensesDaily.Add(expense);
        bottlesDaily.Add(bottles);
        cashDaily.Add(cash);

        _prevCash = cash;
        // (Optionally clamp history length)
        int maxDays = 370;
        if (revenueDaily.Count > maxDays) { revenueDaily.RemoveAt(0); expensesDaily.RemoveAt(0); bottlesDaily.RemoveAt(0); cashDaily.RemoveAt(0); }
    }

    // Helpers
    public int[] GetRevenueLastDays(int days)  => Tail(revenueDaily, days);
    public int[] GetExpensesLastDays(int days) => Tail(expensesDaily, days);
    public int[] GetBottlesLastDays(int days)  => Tail(bottlesDaily, days);
    public int[] GetCashLastDays(int days)     => Tail(cashDaily, days);

    private int[] Tail(List<int> src, int days)
    {
        if (src == null || src.Count == 0) return System.Array.Empty<int>();
        days = Mathf.Clamp(days, 0, src.Count);
        return src.GetRange(src.Count - days, days).ToArray();
    }
}