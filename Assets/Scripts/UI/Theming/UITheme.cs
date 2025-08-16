using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "UITheme", menuName = "WineSim/UI Theme")]
public class UITheme : ScriptableObject
{
    [Header("Typography")]
    public TMP_FontAsset font;                  // e.g., Inter/Roboto TMP asset
    [Range(12, 64)] public int h1 = 36;
    [Range(12, 64)] public int h2 = 28;
    [Range(10, 48)] public int body = 22;
    [Range(8,  32)] public int caption = 18;

    [Header("Palette")]
    public Color bgCanvas    = new Color(0.97f, 0.96f, 0.95f);   // #F7F5F2
    public Color bgPanel     = new Color(0.92f, 0.88f, 0.84f);   // #EAE1D6
    public Color textPrimary = new Color(0.12f, 0.10f, 0.08f);
    public Color textMuted   = new Color(0.30f, 0.27f, 0.24f);
    public Color accent      = new Color(0.61f, 0.76f, 0.48f);   // leaf green
    public Color primary     = new Color(0.42f, 0.31f, 0.14f);   // oak brown
    public Color primaryHi   = new Color(0.52f, 0.41f, 0.24f);
    public Color danger      = new Color(0.86f, 0.36f, 0.36f);

    [Header("Buttons")]
    public Color btnPrimaryBg = new Color(0.42f, 0.31f, 0.14f);
    public Color btnPrimaryFg = Color.white;
    public Color btnSecondaryBg = new Color(0.90f, 0.93f, 0.98f);
    public Color btnSecondaryFg = new Color(0.10f, 0.12f, 0.16f);

    [Header("Spacing")]
    public int pad = 16;           // default padding
    public int gap = 12;           // default spacing
    public int cardMinHeight = 160;

    [Header("Buttons Size")]
    public int btnHeightS = 48;
    public int btnHeightM = 64;
    public int btnHeightL = 80;
    public int btnMinWidth = 160;
}