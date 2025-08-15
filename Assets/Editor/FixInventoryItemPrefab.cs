#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FixInventoryItemPrefab
{
    [MenuItem("Tools/WineSim/Fix Selected Inventory Item Prefab")]
    public static void FixSelected()
    {
        var go = Selection.activeGameObject;
        if (!go) { Debug.LogWarning("Select InventoryItemPrefab in Project first."); return; }

        Undo.RegisterFullObjectHierarchyUndo(go, "Fix Inventory Item Prefab");

        var rt = go.GetComponent<RectTransform>();
        if (!rt) rt = go.AddComponent<RectTransform>();

        // Ensure root has a LayoutElement with a solid height
        var rootLE = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        rootLE.minHeight = 0;
        rootLE.preferredHeight = 110;
        rootLE.flexibleHeight = 0;
        rootLE.flexibleWidth = 1;

        // Horizontal row layout on root
        var row = go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(12, 12, 8, 8);
        row.spacing = 8;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = false;

        // Find/create Left container for Title+Info
        var leftTr = go.transform.Find("Left") as RectTransform;
        if (!leftTr)
        {
            var leftGO = new GameObject("Left", typeof(RectTransform));
            leftTr = leftGO.GetComponent<RectTransform>();
            leftTr.SetParent(go.transform, false);
            leftTr.SetSiblingIndex(0);
        }
        var leftV = leftTr.GetComponent<VerticalLayoutGroup>() ?? leftTr.gameObject.AddComponent<VerticalLayoutGroup>();
        leftV.spacing = 4;
        leftV.childControlWidth = true;
        leftV.childControlHeight = true;
        leftV.childForceExpandWidth = true;
        leftV.childForceExpandHeight = false;

        // Move Title/Info under Left if they exist at root
        MoveIfChildExists(go.transform, leftTr, "Title");
        MoveIfChildExists(go.transform, leftTr, "Info");
        // Optional: hide/remove stray "Label"
        var label = go.transform.Find("Label");
        if (label) label.gameObject.SetActive(false);

        // Fix SellBtn sizing/graphic
        var sell = go.transform.Find("SellBtn") as RectTransform;
        if (sell)
        {
            sell.anchorMin = new Vector2(0.5f, 0.5f);
            sell.anchorMax = new Vector2(0.5f, 0.5f);
            sell.pivot = new Vector2(0.5f, 0.5f);
            sell.anchoredPosition = Vector2.zero; // layout will position it

            var le = sell.GetComponent<LayoutElement>() ?? sell.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 140;
            le.preferredHeight = 64;
            le.flexibleWidth = 0;
            le.flexibleHeight = 0;

            var img = sell.GetComponent<Image>() ?? sell.gameObject.AddComponent<Image>();
            img.raycastTarget = true; // button needs a Graphic
            if (!sell.GetComponent<Button>()) sell.gameObject.AddComponent<Button>();
        }

        EditorUtility.SetDirty(go);
        Debug.Log("Inventory item prefab normalized. Rows will now render with a stable height and layout.");
    }

    static void MoveIfChildExists(Transform root, Transform target, string name)
    {
        var t = root.Find(name);
        if (t != null && t.parent == root) t.SetParent(target, false);
    }
}
#endif