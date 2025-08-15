#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FixInventoryScreenLayout
{
    [MenuItem("Tools/WineSim/Fix Inventory Screen Layout (selected)")]
    public static void FixSelected()
    {
        var root = Selection.activeGameObject;
        if (!root) { Debug.LogWarning("Select InventoryScreen in Hierarchy."); return; }

        Undo.RegisterFullObjectHierarchyUndo(root, "Fix Inventory Screen Layout");

        var vlg = root.GetComponent<VerticalLayoutGroup>() ?? root.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16,16,16,16);
        vlg.spacing = 12;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;

        var bgImg = root.GetComponent<Image>();
        if (bgImg) bgImg.raycastTarget = false;

        var scroll = root.transform.Find("StockScroll")?.GetComponent<ScrollRect>();
        if (scroll)
        {
            var le = scroll.GetComponent<LayoutElement>() ?? scroll.gameObject.AddComponent<LayoutElement>();
            le.flexibleHeight = 1; le.preferredHeight = -1; le.minHeight = 0;

            var rt = scroll.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);

            var viewport = scroll.viewport ? scroll.viewport : scroll.transform.Find("Viewport")?.GetComponent<RectTransform>();
            if (viewport)
            {
                viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
                viewport.offsetMin = Vector2.zero; viewport.offsetMax = Vector2.zero;

                var mask = viewport.GetComponent<Mask>(); if (mask) mask.showMaskGraphic = false;
            }

            var content = scroll.content ? scroll.content : scroll.transform.Find("Viewport/Content")?.GetComponent<RectTransform>();
            if (content)
            {
                content.anchorMin = new Vector2(0,1);
                content.anchorMax = new Vector2(1,1);
                content.pivot = new Vector2(0.5f,1);
                var vlgC = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlgC.padding = new RectOffset(12,12,8,16);
                vlgC.spacing = 8;
                vlgC.childControlWidth = true;
                vlgC.childControlHeight = true;
                vlgC.childForceExpandWidth = true;
                vlgC.childForceExpandHeight = false;

                var fit = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        Debug.Log("InventoryScreen layout fixed. If rows still clip, check each row prefab’s LayoutElement height.");
    }
}
#endif