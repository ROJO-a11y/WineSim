// Assets/Scripts/Core/TimeController.cs
using UnityEngine;
using System;

[DefaultExecutionOrder(-100)] // ensure this initializes before most UI
public class TimeController : MonoBehaviour
{
    public static TimeController I { get; private set; }
    public event Action OnNewDay;

    public float DayProgress01
{
    get
    {
        if (cfg == null || cfg.secondsPerDay <= 0f) return 0f;
        return Mathf.Clamp01(t / cfg.secondsPerDay);
    }
}

    [Header("Debug")]
    [Tooltip("Editor-only convenience: when ON, the save file will be cleared when entering Play mode.")]
    [SerializeField] private bool resetOnPlay = false; // optional, safe in builds

    [Header("State")]
    public int Day { get; private set; }
    public int Year => Mathf.FloorToInt((float)Day / cfg.daysPerYear);
    public int DayOfYear => Day % cfg.daysPerYear;

    [Header("Config")]
    [SerializeField] private GameConfig cfg; // assigned via holder or directly

    private float t;

    void Awake()
    {
        I = this;
    }

    void Start()
    {
        // Optional editor convenience: start fresh
        #if UNITY_EDITOR
        if (resetOnPlay)
        {
            SaveLoadManager.Delete();
            Debug.Log("TimeController: ResetOnPlay → cleared save.");
        }
        #endif

        var saved = SaveLoadManager.Load();
        if (saved != null)
        {
            // Load persistent state
            Day = saved.day;
            EconomySystem.I.LoadFromSave(saved);
            VineyardSystem.I.LoadFromSave(saved);
            ProductionSystem.I.LoadFromSave(saved);
            InventorySystem.I.LoadFromSave(saved);

            // Offline catch-up (capped)
            int offlineDays = Mathf.Min(
                (int)((DateTime.UtcNow - saved.utcSavedAt).TotalSeconds / Mathf.Max(0.001f, cfg.secondsPerDay)),
                Mathf.Max(0, cfg.offlineCatchupCapDays)
            );
            for (int i = 0; i < offlineDays; i++) SimulateOneDay();
        }
        else
        {
            // New game bootstrap
            Day = 0;
            EconomySystem.I.InitNewGame();
            VineyardSystem.I.InitNewGame();
            ProductionSystem.I.InitNewGame();
            InventorySystem.I.InitNewGame();
        }
    }

    void Update()
    {
        // 1 in-game day per cfg.secondsPerDay (e.g., 2 seconds)
        t += Time.unscaledDeltaTime;
        while (t >= cfg.secondsPerDay)
        {
            t -= cfg.secondsPerDay;
            SimulateOneDay();
        }
    }

    private void SimulateOneDay()
    {
        WeatherSystem.I?.TickDaily(DayOfYear);   // ← keep this
        VineyardSystem.I?.TickDaily();
        ProductionSystem.I?.TickDaily();
        InventorySystem.I?.TickDaily();
        EconomySystem.I?.TickDaily();

        Day++;
        OnNewDay?.Invoke();
    }
    public void SaveNow()
    {
        SaveLoadManager.Save(SaveData.FromSystems(Day));
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) SaveNow();
    }

    void OnApplicationQuit()
    {
        SaveNow();
    }
}