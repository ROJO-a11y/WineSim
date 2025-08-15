#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public static class FixInventoryZOrder
{
    [MenuItem("Tools/WineSim/Fix Inventory Z-Order (selected)")]
    public static void FixSelected()
    {
        var root = Selection.activeGameObject;
        if (!root) { Debug.LogWarning("Select your InventoryScreen GameObject, then run this."); return; }

        Undo.RegisterFullObjectHierarchyUndo(root, "Fix Inventory Z-Order");

        // Find ScrollRect under this screen
        var scroll = root.GetComponentInChildren<ScrollRect>(true);
        if (!scroll) { Debug.LogError("No ScrollRect found under selection."); return; }

        var scrollRT = scroll.GetComponent<RectTransform>();
        var viewport = scroll.viewport ? scroll.viewport : scroll.transform.Find("Viewport")?.GetComponent<RectTransform>();
        var content  = scroll.content  ? scroll.content  : scroll.transform.Find("Viewport/Content")?.GetComponent<RectTransform>();

        // Ensure viewport/content exist
        if (!viewport)
        {
            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            vp.transform.SetParent(scroll.transform, false);
            viewport = vp.GetComponent<RectTransform>();
            scroll.viewport = viewport;
        }
        if (!content)
        {
            var ct = new GameObject("Content", typeof(RectTransform));
            ct.transform.SetParent(viewport, false);
            content = ct.GetComponent<RectTransform>();
            scroll.content = content;
        }

        // Put ScrollRect on top (last sibling)
        scrollRT.SetAsLastSibling();

        // Disable 'Show Mask Graphic' so the viewport doesn’t hide content color
        var mask = viewport.GetComponent<Mask>();
        if (mask) mask.showMaskGraphic = false;
        var img = viewport.GetComponent<Image>();
        if (img) img.color = new Color(0,0,0,0); // transparent

        // Find likely background panels (Images not part of the ScrollRect subtree)
        var allImages = root.GetComponentsInChildren<Image>(true)
                           .Where(i => !i.transform.IsChildOf(scrollRT))
                           .ToList();

        foreach (var im in allImages)
        {
            // Turn off raycast so background doesn’t block clicks
            im.raycastTarget = false;

            // If this looks like a big background panel, push it down in draw order
            var rt = im.rectTransform;
            if (rt.parent == root.transform) // direct child of screen
            {
                rt.SetAsFirstSibling(); // background under everything
            }

            // If this Image has a Canvas with override sorting, normalize it
            var subCanvas = im.GetComponent<Canvas>();
            if (subCanvas && subCanvas.overrideSorting)
            {
                subCanvas.overrideSorting = false;
                subCanvas.sortingOrder = 0;
            }
        }

        // Final: ensure Content is top-stretch, pivot top, so it grows downward
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot     = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;

        Debug.Log("Inventory z-order fixed: ScrollRect moved to front, background raycasts disabled, masks normalized.", root);
        EditorUtility.SetDirty(root);
    }
}
#endif