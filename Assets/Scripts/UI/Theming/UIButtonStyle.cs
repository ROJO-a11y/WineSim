using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class UIButtonStyle : MonoBehaviour
{
    public enum Variant { Primary, Secondary, Danger, Ghost }
    public enum Size { S, M, L }

    public UITheme theme;
    public Variant variant = Variant.Primary;
    public Size size = Size.M;

    Image _bg;
    TMP_Text _label;
    RectTransform _rt;
    LayoutElement _le;

    void Reset(){ Cache(); }
    void OnValidate(){ Apply(); }
    void Awake(){ Apply(); }

    void Cache()
    {
        _bg = GetComponent<Image>();
        _rt = GetComponent<RectTransform>();
        _le = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        _label = GetComponentInChildren<TMP_Text>(true);
    }

    public void Apply()
    {
        if (!_bg || !_rt || !_le) Cache();
        if (!theme) return;

        // Size
        int h = size == Size.S ? theme.btnHeightS : size == Size.M ? theme.btnHeightM : theme.btnHeightL;
        _le.minHeight = h;
        _le.preferredHeight = h;
        _le.minWidth = theme.btnMinWidth;

        // Variant
        Color bg, fg;
        switch (variant)
        {
            default:
            case Variant.Primary:  bg = theme.btnPrimaryBg; fg = theme.btnPrimaryFg; break;
            case Variant.Secondary:bg = theme.btnSecondaryBg; fg = theme.btnSecondaryFg; break;
            case Variant.Danger:   bg = theme.danger; fg = Color.white; break;
            case Variant.Ghost:    bg = new Color(0,0,0,0); fg = theme.primary; break;
        }
        _bg.color = bg;
        if (_label)
        {
            _label.font = theme.font;
            _label.fontSize = theme.body;
            _label.color = fg;
        }

        // States (simple)
        var colors = GetComponent<Button>().colors;
        colors.normalColor = bg;
        colors.highlightedColor = Color.Lerp(bg, Color.white, 0.08f);
        colors.pressedColor = Color.Lerp(bg, Color.black, 0.10f);
        colors.disabledColor = new Color(bg.r, bg.g, bg.b, 0.4f);
        GetComponent<Button>().colors = colors;
    }
}