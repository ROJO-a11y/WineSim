using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryItemUI : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text Title;
    public TMP_Text Info;
    public Button SellButton;

    // current data + callback
    BottleEntryState _entry;
    Action<BottleEntryState> _onSell;

    void Awake()
    {
        // Fallback auto-cache so prefab renames don't break
        if (!Title)     Title     = transform.Find("Title")?.GetComponent<TMP_Text>();
        if (!Info)      Info      = transform.Find("Info")?.GetComponent<TMP_Text>();
        if (!SellButton)SellButton= transform.Find("SellBtn")?.GetComponent<Button>();
    }

    public void Setup(BottleEntryState entry, Action<BottleEntryState> onSell)
    {
        _entry  = entry;
        _onSell = onSell;
        Refresh();

        // Rebind listener each setup (pool-safe)
        if (SellButton)
        {
            SellButton.onClick.RemoveAllListeners();
            SellButton.onClick.AddListener(() =>
            {
                // debug to confirm wiring
                Debug.Log($"InventoryItemUI: Sell clicked → {_entry?.id}", this);
                _onSell?.Invoke(_entry);
            });

            SellButton.interactable = (_entry != null && (_entry.bottles > 0));
        }
    }

    public void Refresh()
    {
        if (_entry == null) return;

        string variety = string.IsNullOrEmpty(_entry.varietyName) ? "Wine" : _entry.varietyName;
        int vintage    = _entry.vintageYear;
        float q        = _entry.quality;

        if (Title) Title.text = $"{variety} {vintage}";
        if (Info)  Info.text  = $"Bottles: {_entry.bottles}\nQuality: {q:0.0}";
    }
#if UNITY_EDITOR
    // Flexible Editor wiring helper used by WineSimInventoryUIBuilder
    public void __EditorAssign(Transform root = null)
    {
        if (root == null) root = this.transform;
        if (!Title)      Title      = root.Find("Title")?.GetComponent<TMPro.TMP_Text>();
        if (!Info)       Info       = root.Find("Info")?.GetComponent<TMPro.TMP_Text>();
        if (!SellButton) SellButton = root.Find("SellBtn")?.GetComponent<UnityEngine.UI.Button>()
                                    ?? root.GetComponentInChildren<UnityEngine.UI.Button>(true);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void __EditorAssign(TMPro.TMP_Text title, TMPro.TMP_Text info, UnityEngine.UI.Button sell)
    {
        Title = title; Info = info; SellButton = sell;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void __EditorAssign(UnityEngine.GameObject titleGO, UnityEngine.GameObject infoGO, UnityEngine.GameObject sellGO)
    {
        __EditorAssign(
            titleGO ? titleGO.GetComponent<TMPro.TMP_Text>() : null,
            infoGO  ? infoGO.GetComponent<TMPro.TMP_Text>() : null,
            sellGO  ? sellGO.GetComponent<UnityEngine.UI.Button>() : null
        );
    }
#endif
}