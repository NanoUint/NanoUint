using NanoUint.Drawing;

namespace NanoUint;

/// <summary>简易文本渲染器。显示纯文本字符串。</summary>
public sealed class TextRenderer : Component
{
    private string _content = "";
    private float _fontSize = 18;
    private Color _textColor = Color.White;

    public string Content
    {
        get => _content;
        set { if (_content != value) { _content = value; MarkDirty(); } }
    }

    public float FontSize
    {
        get => _fontSize;
        set { if (!_fontSize.Equals(value)) { _fontSize = value; MarkDirty(); } }
    }

    public Color TextColor
    {
        get => _textColor;
        set { if (!_textColor.Equals(value)) { _textColor = value; MarkDirty(); } }
    }

    public TextRenderer()
    {
        if (GameObject?.Transform != null)
            GameObject.Transform.SortingOrder = 200;
    }
}
