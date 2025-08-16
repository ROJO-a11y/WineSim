using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UICardStyle : MonoBehaviour
{
    public UITheme theme;
    public int padOverride = -1; // -1 = use theme.pad

    void OnValidate(){ Apply(); }
    void Awake(){ Apply(); }

    public void Apply()
    {
        var img = GetComponent<Image>();
        if (!img || !theme) return;
        img.color = theme.bgPanel;

        // Ensure a layout group with padding & spacing
        var vlg = GetComponent<VerticalLayoutGroup>();
        var hlg = GetComponent<HorizontalLayoutGroup>();
        int pad = padOverride >= 0 ? padOverride : theme.pad;

        if (vlg)
        {
            vlg.padding = new RectOffset(pad, pad, pad, pad);
            vlg.spacing = theme.gap;
        }
        else
        {
            if (!hlg) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(pad, pad, pad, pad);
            hlg.spacing = theme.gap;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
        }

        var le = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        le.minHeight = theme.cardMinHeight;
    }
}