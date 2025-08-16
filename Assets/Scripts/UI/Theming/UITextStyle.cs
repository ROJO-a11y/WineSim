using UnityEngine;
using TMPro;

public class UITextStyle : MonoBehaviour
{
    public enum Kind { Heading1, Heading2, Body, Caption, Muted }
    public Kind kind = Kind.Body;
    public UITheme theme;

    TMP_Text _tmp;

    void Reset(){ _tmp = GetComponent<TMP_Text>(); }
    void OnValidate(){ Apply(); }
    void Awake(){ Apply(); }

    public void Apply()
    {
        if (!_tmp) _tmp = GetComponent<TMP_Text>();
        if (!theme || !_tmp) return;

        _tmp.font = theme.font;
        _tmp.enableAutoSizing = false;
        _tmp.fontStyle = FontStyles.Normal;       // reset styles each apply
        _tmp.fontWeight = FontWeight.Regular;     // use TMP's FontWeight (SemiBold, Bold, etc.)

        switch(kind)
        {
            case Kind.Heading1:
                _tmp.fontSize = theme.h1;
                _tmp.color = theme.textPrimary;
                _tmp.fontWeight = FontWeight.Bold;
                break;
            case Kind.Heading2:
                _tmp.fontSize = theme.h2;
                _tmp.color = theme.textPrimary;
                _tmp.fontWeight = FontWeight.SemiBold;   // <-- was FontStyles.SemiBold (invalid)
                break;
            case Kind.Body:
                _tmp.fontSize = theme.body;
                _tmp.color = theme.textPrimary;
                _tmp.fontWeight = FontWeight.Regular;
                break;
            case Kind.Caption:
                _tmp.fontSize = theme.caption;
                _tmp.color = theme.textPrimary;
                _tmp.fontStyle = FontStyles.Italic;
                _tmp.fontWeight = FontWeight.Regular;
                break;
            case Kind.Muted:
                _tmp.fontSize = theme.body;
                _tmp.color = theme.textMuted;
                _tmp.fontWeight = FontWeight.Regular;
                break;
        }
    }
}
