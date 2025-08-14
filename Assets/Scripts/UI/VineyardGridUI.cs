using System; // <-- for Action
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VineyardGridUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RectTransform gridRoot;          // parent with GridLayoutGroup
    [SerializeField] VineyardTileUI tilePrefab;       // must have Button + VineyardTileUI
    [SerializeField] VineyardDetailPanel detailPanel; // optional; if assigned, opens on click

    private readonly List<VineyardTileUI> tiles = new();
    private int builtForCount = -1;

    void Reset()  { AutoCache(); }
    void OnValidate() { if (!Application.isPlaying) AutoCache(); }

    void OnEnable()
    {
        AutoCache();
        BuildIfNeeded();
        RefreshAll();

        if (TimeController.I) TimeController.I.OnNewDay += RefreshAll;
    }
    void OnDisable()
    {
        if (TimeController.I) TimeController.I.OnNewDay -= RefreshAll;
    }

    private void AutoCache()
    {
        if (!gridRoot) gridRoot = GetComponent<RectTransform>();
        var grid = gridRoot ? gridRoot.GetComponent<GridLayoutGroup>() : null;
        if (grid == null && gridRoot != null)
        {
            grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(220, 220);
            grid.spacing = new Vector2(12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6; // 6x4
        }
    }

    private void BuildIfNeeded()
    {
        if (VineyardSystem.I == null || tilePrefab == null || gridRoot == null) return;

        int want = Mathf.Max(1, VineyardSystem.I.Tiles?.Length ?? 0);
        if (want == builtForCount) return;

        for (int i = gridRoot.childCount - 1; i >= 0; i--)
            Destroy(gridRoot.GetChild(i).gameObject);
        tiles.Clear();

        for (int i = 0; i < want; i++)
        {
            var tile = Instantiate(tilePrefab, gridRoot);
            tile.Bind(i);

            tile.OnClicked     -= OnTileClicked;
            tile.OnClicked     += OnTileClicked;
            tile.OnClickedIndex-= OnTileClicked;
            tile.OnClickedIndex+= OnTileClicked;

            tiles.Add(tile);
        }

        builtForCount = want;
    }

    private void RefreshAll()
    {
        BuildIfNeeded();
        for (int i = 0; i < tiles.Count; i++)
            tiles[i].Refresh(); // alias to RefreshImmediate()
    }

    private void OnTileClicked(VineyardTileUI tile)
    {
        if (!tile) return;
        OpenDetail(tile.TileIndex);
    }

    private void OnTileClicked(int tileIndex)
    {
        OpenDetail(tileIndex);
    }

    private void OpenDetail(int index)
    {
        if (!detailPanel) return;

        // Pass a callback so the grid refreshes after buy/plant/harvest actions in the panel
        detailPanel.Open(index, RefreshAll);
        // If you want to refresh just the clicked tile:
        // detailPanel.Open(index, () => { if (index >= 0 && index < tiles.Count) tiles[index].Refresh(); });
    }
}