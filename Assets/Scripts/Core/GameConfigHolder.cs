using UnityEngine;

public class GameConfigHolder : MonoBehaviour
{
    public static GameConfigHolder Instance { get; private set; }

    [SerializeField] private GameConfig config;   // ← drag your GameConfig asset here
    public GameConfig Config => config;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // survives scene loads
    }
}