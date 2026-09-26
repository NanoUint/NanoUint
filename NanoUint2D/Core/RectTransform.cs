using System;
using System.Runtime.CompilerServices;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>UI Transform that adds an anchor and pivot system on top of the base Transform.</summary>
public sealed class RectTransform : Transform
{
    private Vector2 _anchorMin = new(0.5f, 0.5f);
    private Vector2 _anchorMax = new(0.5f, 0.5f);
    private Vector2 _pivot = new(0.5f, 0.5f);
    private Vector2 _sizeDelta;
    private Vector2 _anchoredPosition;

    /// <summary>Minimum anchor (0~1, relative to the parent).</summary>
    public Vector2 AnchorMin
    {
        get => _anchorMin;
        set { _anchorMin = Clamp01(value); MarkDirty(); }
    }

    /// <summary>Maximum anchor (0~1).</summary>
    public Vector2 AnchorMax
    {
        get => _anchorMax;
        set { _anchorMax = Clamp01(value); MarkDirty(); }
    }

    /// <summary>Pivot (0~1); default (0.5, 0.5) is the center.</summary>
    public Vector2 Pivot
    {
        get => _pivot;
        set { _pivot = Clamp01(value); MarkDirty(); }
    }

    /// <summary>Offset size relative to the anchor area.</summary>
    public Vector2 SizeDelta
    {
        get => _sizeDelta;
        set { if (!_sizeDelta.Equals(value)) { _sizeDelta = value; MarkDirty(); } }
    }

    /// <summary>Offset position relative to the anchor.</summary>
    public Vector2 AnchoredPosition
    {
        get => _anchoredPosition;
        set { if (!_anchoredPosition.Equals(value)) { _anchoredPosition = value; MarkDirty(); } }
    }

    /// <summary>Applies an anchor preset.</summary>
    public void SetAnchor(AnchorPreset preset)
    {
        switch (preset)
        {
            case AnchorPreset.TopLeft:        _anchorMin = _anchorMax = _pivot = new Vector2(0f, 0f); break;
            case AnchorPreset.TopCenter:      _anchorMin = _anchorMax = _pivot = new Vector2(0.5f, 0f); break;
            case AnchorPreset.TopRight:       _anchorMin = _anchorMax = _pivot = new Vector2(1f, 0f); break;
            case AnchorPreset.MiddleLeft:     _anchorMin = _anchorMax = _pivot = new Vector2(0f, 0.5f); break;
            case AnchorPreset.MiddleCenter:   _anchorMin = _anchorMax = _pivot = new Vector2(0.5f, 0.5f); break;
            case AnchorPreset.MiddleRight:    _anchorMin = _anchorMax = _pivot = new Vector2(1f, 0.5f); break;
            case AnchorPreset.BottomLeft:     _anchorMin = _anchorMax = _pivot = new Vector2(0f, 1f); break;
            case AnchorPreset.BottomCenter:   _anchorMin = _anchorMax = _pivot = new Vector2(0.5f, 1f); break;
            case AnchorPreset.BottomRight:    _anchorMin = _anchorMax = _pivot = new Vector2(1f, 1f); break;
            case AnchorPreset.StretchFull:    _anchorMin = Vector2.Zero; _anchorMax = Vector2.One; _pivot = new Vector2(0.5f, 0.5f); break;
            case AnchorPreset.StretchTop:     _anchorMin = new Vector2(0f, 0f); _anchorMax = new Vector2(1f, 0f); _pivot = new Vector2(0.5f, 0f); break;
            case AnchorPreset.StretchMiddle:  _anchorMin = new Vector2(0f, 0.5f); _anchorMax = new Vector2(1f, 0.5f); _pivot = new Vector2(0.5f, 0.5f); break;
            case AnchorPreset.StretchBottom:  _anchorMin = new Vector2(0f, 1f); _anchorMax = new Vector2(1f, 1f); _pivot = new Vector2(0.5f, 1f); break;
            case AnchorPreset.StretchLeft:    _anchorMin = new Vector2(0f, 0f); _anchorMax = new Vector2(0f, 1f); _pivot = new Vector2(0f, 0.5f); break;
            case AnchorPreset.StretchCenter:  _anchorMin = new Vector2(0.5f, 0f); _anchorMax = new Vector2(0.5f, 1f); _pivot = new Vector2(0.5f, 0.5f); break;
            case AnchorPreset.StretchRight:   _anchorMin = new Vector2(1f, 0f); _anchorMax = new Vector2(1f, 1f); _pivot = new Vector2(1f, 0.5f); break;
        }
        MarkDirty();
        GameObject?.MarkComponentsDirty();
    }

    /// <summary>Whether the element stretches on an axis.</summary>
    public bool IsStretchX => Math.Abs(_anchorMin.X - _anchorMax.X) > 0.001f;
    public bool IsStretchY => Math.Abs(_anchorMin.Y - _anchorMax.Y) > 0.001f;

    /// <summary>Computes the anchor's reference position within the parent, given the parent's width and height.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (float anchorX, float anchorY) GetAnchorPosition(float parentW, float parentH)
    {
        float ax = parentW * (IsStretchX ? (_anchorMin.X + _anchorMax.X) * 0.5f : _anchorMin.X);
        float ay = parentH * (IsStretchY ? (_anchorMin.Y + _anchorMax.Y) * 0.5f : _anchorMin.Y);
        return (ax, ay);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal (double left, double top) ComputeCanvasPosition(double canvasW, double canvasH, double elemW, double elemH)
    {
        float ax = _anchorMin.X * (float)canvasW;
        float ay = _anchorMin.Y * (float)canvasH;

        if (IsStretchX)
        {
            // Stretch mode: left = anchorMin position + anchoredPosition.X
            // width = (anchorMax - anchorMin) * parentW + sizeDelta.X
            ax += _anchoredPosition.X;
        }
        else
        {
            // Point-anchor mode: left = anchorX - pivot * elemW + anchoredPosition.X
            ax = (float)canvasW * _anchorMin.X - _pivot.X * (float)elemW + _anchoredPosition.X;
        }

        if (IsStretchY)
        {
            ay += _anchoredPosition.Y;
        }
        else
        {
            ay = (float)canvasH * _anchorMin.Y - _pivot.Y * (float)elemH + _anchoredPosition.Y;
        }

        return (ax, ay);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 Clamp01(Vector2 v)
        => new(Math.Clamp(v.X, 0f, 1f), Math.Clamp(v.Y, 0f, 1f));
}

/// <summary>Anchor presets for positioning UI elements.</summary>
public enum AnchorPreset
{

    #region Fixed anchors (AnchorMin == AnchorMax)
    TopLeft, TopCenter, TopRight,
    MiddleLeft, MiddleCenter, MiddleRight,
    BottomLeft, BottomCenter, BottomRight,

    #endregion

    #region Stretch modes
    /// <summary>Stretches to fill the whole parent.</summary>
    StretchFull,
    /// <summary>Stretches width, anchored at the top.</summary>
    StretchTop,
    /// <summary>Stretches width, anchored at the middle.</summary>
    StretchMiddle,
    /// <summary>Stretches width, anchored at the bottom.</summary>
    StretchBottom,
    /// <summary>Stretches height, anchored at the left.</summary>
    StretchLeft,
    /// <summary>Stretches height, anchored at the center.</summary>
    StretchCenter,
    /// <summary>Stretches height, anchored at the right.</summary>
    StretchRight,
    #endregion
}
