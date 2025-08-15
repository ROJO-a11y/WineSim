#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Text;

public static class DumpUIHierarchy
{
    [MenuItem("Tools/WineSim/Dump UI Hierarchy (selected)")]
    public static void DumpSelected()
    {
        var root = Selection.activeTransform;
        if (!root)
        {
            Debug.LogWarning("Select your InventoryScreen GameObject (or any UI root) first.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"=== UI HIERARCHY DUMP: {root.name} ===");
        sb.AppendLine($"Scene: {root.gameObject.scene.name}");
        sb.AppendLine($"Path:  {GetPath(root)}");
        sb.AppendLine();

        DumpNode(root, sb, 0);

        // Save file
        var dir = "Assets/HierarchyDumps";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var safeName = Sanitize(root.name);
        var path = $"{dir}/{safeName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        AssetDatabase.ImportAsset(path);

        // Copy to clipboard
        EditorGUIUtility.systemCopyBuffer = sb.ToString();

        Debug.Log($"UI hierarchy dumped to:\n{path}\n(also copied to clipboard)");
    }

    static void DumpNode(Transform t, StringBuilder sb, int depth)
    {
        string indent = new string(' ', depth * 2);
        var go = t.gameObject;

        // Line header
        sb.Append(indent).Append("• ").Append(go.name);

        // Basic component flags
        bool hasBtn = go.GetComponent<Button>();
        bool hasImg = go.GetComponent<Image>();
        bool hasText = go.GetComponent<Text>() || go.GetComponent<TMP_Text>();
        bool hasSR = go.GetComponent<ScrollRect>();
        bool hasVLG = go.GetComponent<VerticalLayoutGroup>();
        bool hasHLG = go.GetComponent<HorizontalLayoutGroup>();
        bool hasCSF = go.GetComponent<ContentSizeFitter>();
        bool hasLE  = go.GetComponent<LayoutElement>();
        bool hasMask = go.GetComponent<Mask>() || go.GetComponent<RectMask2D>();
        bool hasCanvas = go.GetComponent<Canvas>();

        sb.Append("  [");
        if (hasBtn) sb.Append("Button ");
        if (hasImg) sb.Append("Image ");
        if (hasText) sb.Append("Text ");
        if (hasSR) sb.Append("ScrollRect ");
        if (hasVLG) sb.Append("VLG ");
        if (hasHLG) sb.Append("HLG ");
        if (hasCSF) sb.Append("CSF ");
        if (hasLE) sb.Append("LE ");
        if (hasMask) sb.Append("Mask ");
        if (hasCanvas) sb.Append("Canvas ");
        sb.AppendLine("]");

        // RectTransform details
        var rt = t as RectTransform;
        if (rt)
        {
            sb.AppendLine(indent + $"   Rect: anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot} sizeDelta={rt.sizeDelta} anchoredPos={rt.anchoredPosition}");
        }

        // Selected component details (small but useful)
        var img = go.GetComponent<Image>();
        if (img) sb.AppendLine(indent + $"   Image: raycastTarget={img.raycastTarget} color={img.color}");

        var sr = go.GetComponent<ScrollRect>();
        if (sr)
        {
            var vpPath = sr.viewport ? GetPath(sr.viewport) : "(null)";
            var ctPath = sr.content ? GetPath(sr.content) : "(null)";
            sb.AppendLine(indent + $"   ScrollRect: vertical={sr.vertical} horizontal={sr.horizontal} viewport={vpPath} content={ctPath}");
        }

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        if (vlg)
            sb.AppendLine(indent + $"   VLG: padding=({vlg.padding.left},{vlg.padding.right},{vlg.padding.top},{vlg.padding.bottom}) spacing={vlg.spacing} controlW/H={vlg.childControlWidth}/{vlg.childControlHeight} expandW/H={vlg.childForceExpandWidth}/{vlg.childForceExpandHeight}");

        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        if (hlg)
            sb.AppendLine(indent + $"   HLG: padding=({hlg.padding.left},{hlg.padding.right},{hlg.padding.top},{hlg.padding.bottom}) spacing={hlg.spacing} controlW/H={hlg.childControlWidth}/{hlg.childControlHeight} expandW/H={hlg.childForceExpandWidth}/{hlg.childForceExpandHeight}");

        var csf = go.GetComponent<ContentSizeFitter>();
        if (csf)
            sb.AppendLine(indent + $"   CSF: HFit={csf.horizontalFit} VFit={csf.verticalFit}");

        var le = go.GetComponent<LayoutElement>();
        if (le)
            sb.AppendLine(indent + $"   LE: min=({le.minWidth},{le.minHeight}) pref=({le.preferredWidth},{le.preferredHeight}) flex=({le.flexibleWidth},{le.flexibleHeight})");

        var mask = go.GetComponent<Mask>();
        if (mask) sb.AppendLine(indent + $"   Mask: showGraphic={mask.showMaskGraphic}");
        var rmask = go.GetComponent<RectMask2D>();
        if (rmask) sb.AppendLine(indent + "   RectMask2D: (enabled)");

        var canv = go.GetComponent<Canvas>();
        if (canv) sb.AppendLine(indent + $"   Canvas: overrideSorting={canv.overrideSorting} order={canv.sortingOrder}");

        var tmp = go.GetComponent<TMP_Text>();
        if (tmp) sb.AppendLine(indent + $"   TMP_Text: text=\"{Trunc(tmp.text)}\" overflow={tmp.overflowMode} wrap={tmp.enableWordWrapping}");

        // Recurse
        for (int i = 0; i < t.childCount; i++)
            DumpNode(t.GetChild(i), sb, depth + 1);
    }

    static string GetPath(Transform t)
    {
        if (!t) return "(null)";
        var p = t.name;
        var cur = t.parent;
        while (cur != null) { p = cur.name + "/" + p; cur = cur.parent; }
        return p;
    }

    static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    static string Trunc(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 64 ? s.Substring(0, 64) + "..." : s);
}
#endif