#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FixInventoryLayout
{
    [MenuItem("Tools/WineSim/Fix Inventory Layout (selected)")]
    public static void FixSelected()
    {
        var sel = Selection.activeGameObject;
        if (!sel) { Debug.LogWarning("Select your InventoryScreen GameObject first."); return; }

        // Expect a ScrollRect under the selected screen
        var scroll = sel.GetComponentInChildren<ScrollRect>(true);
        if (!scroll) { Debug.LogError("No ScrollRect found under selection."); return; }

        var scrollRT = scroll.GetComponent<RectTransform>();
        var viewport = scroll.viewport ? scroll.viewport : scroll.transform.Find("Viewport")?.GetComponent<RectTransform>();
        var content  = scroll.content  ? scroll.content  : scroll.transform.Find("Viewport/Content")?.GetComponent<RectTransform>();

        if (!viewport) { var go = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask)); go.transform.SetParent(scroll.transform, false); viewport = go.GetComponent<RectTransform>(); scroll.viewport = viewport; }
        if (!content)  { var go = new GameObject("Content", typeof(RectTransform)); go.transform.SetParent(viewport, false); content = go.GetComponent<RectTransform>(); scroll.content = content; }

        Undo.RegisterFullObjectHierarchyUndo(sel, "Fix Inventory Layout");

        // ScrollRect
        SetStretchFull(scrollRT);
        var img = scroll.GetComponent<Image>() ?? scroll.gameObject.AddComponent<Image>();
        img.color = new Color(0,0,0,0); // transparent but blocks raycasts

        scroll.horizontal = false;
        scroll.vertical   = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.inertia = true;

        // Viewport
        SetStretchFull(viewport);
        var vpMask = viewport.GetComponent<Mask>();
        if (vpMask) vpMask.showMaskGraphic = false;

        // Content
        SetTopStretch(content);
        content.anchoredPosition = new Vector2(0, 0);
        content.sizeDelta = new Vector2(0, 0);

        var vlg = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 8, 16);
        vlg.spacing = 8;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fit = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        Debug.Log("Inventory layout fixed. If items still clip, check each item prefab’s LayoutElement height.", sel);
        EditorUtility.SetDirty(sel);
    }

    static void SetStretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
    static void SetTopStretch(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(1,1); rt.pivot = new Vector2(0.5f,1);
    }
}
#endif