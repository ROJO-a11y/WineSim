#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public static class ApplyUITheme
{
    [MenuItem("Tools/WineSim/Apply Theme To Selected Root")]
    public static void ApplySelected()
    {
        var root = Selection.activeTransform;
        if (!root) { Debug.LogWarning("Select a UI root (e.g., InventoryScreen / FacilityScreen)."); return; }

        var theme = AssetDatabase.LoadAssetAtPath<UITheme>("Assets/Data/UITheme.asset");
        if (!theme) { Debug.LogError("Missing Assets/Data/UITheme.asset (UITheme)."); return; }

        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Apply Theme");

        // Background panels → card or canvas bg
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            var go = img.gameObject;
            bool isBtn = go.GetComponent<Button>();
            bool isScroll = go.GetComponent<ScrollRect>();
            bool isViewport = go.name == "Viewport";
            bool isContent = go.name == "Content";

            if (isBtn || isViewport || isContent) continue;

            if (!go.GetComponent<UICardStyle>() && go.transform.parent == root) // top panel
            {
                img.color = theme.bgCanvas;
                continue;
            }

            // Row/card
            var card = go.GetComponent<UICardStyle>() ?? go.AddComponent<UICardStyle>();
            card.theme = theme;
            card.Apply();
        }

        // Buttons
        foreach (var btn in root.GetComponentsInChildren<Button>(true))
        {
            var style = btn.GetComponent<UIButtonStyle>() ?? btn.gameObject.AddComponent<UIButtonStyle>();
            style.theme = theme;

            // Simple naming convention
            var n = btn.name.ToLower();
            if (n.Contains("sell") || n.Contains("bottle") || n.Contains("buy"))
                style.variant = UIButtonStyle.Variant.Primary;
            else if (n.Contains("cancel") || n.Contains("close"))
                style.variant = UIButtonStyle.Variant.Secondary;
            else
                style.variant = UIButtonStyle.Variant.Secondary;

            style.size = UIButtonStyle.Size.M;
            style.Apply();
        }

        // Text
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            var ts = t.GetComponent<UITextStyle>() ?? t.gameObject.AddComponent<UITextStyle>();
            ts.theme = theme;
            var n = t.name.ToLower();
            if (n.Contains("title") || n.Contains("header")) ts.kind = UITextStyle.Kind.Heading2;
            else if (n.Contains("info") || n.Contains("max") || n.Contains("est"))
                ts.kind = UITextStyle.Kind.Muted;
            else ts.kind = UITextStyle.Kind.Body;
            ts.Apply();
        }

        Debug.Log($"Applied UITheme to: {root.name}");
    }
}
#endif