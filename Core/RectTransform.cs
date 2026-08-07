using System;
using System.Runtime.CompilerServices;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>UI 专用 Transform。在基础 Transform 之上添加锚点（Anchor）和轴心（Pivot）系统。</summary>
public sealed class RectTransform : Transform
{
    private Vector2 _anchorMin = new(0.5f, 0.5f);
    private Vector2 _anchorMax = new(0.5f, 0.5f);
    private Vector2 _pivot = new(0.5f, 0.5f);
    private Vector2 _sizeDelta;
    private Vector2 _anchoredPosition;

    /// <summary>锚点最小值（0~1，相对于父容器）。与 AnchorMax 共同定义元素的参考区域。</summary>
    public Vector2 AnchorMin
    {
        get => _anchorMin;
        set { _anchorMin = Clamp01(value); MarkDirty(); }
    }

    /// <summary>锚点最大值（0~1）。</summary>
    public Vector2 AnchorMax
    {
        get => _anchorMax;
        set { _anchorMax = Clamp01(value); MarkDirty(); }
    }

    /// <summary>轴心点（0~1），元素的旋转/缩放原点。默认 (0.5, 0.5) 即中心。</summary>
    public Vector2 Pivot
    {
        get => _pivot;
        set { _pivot = Clamp01(value); MarkDirty(); }
    }

    /// <summary>相对于锚点区域的偏移尺寸。正值比锚点区域大，负值比锚点区域小。</summary>
    public Vector2 SizeDelta
    {
        get => _sizeDelta;
        set { if (!_sizeDelta.Equals(value)) { _sizeDelta = value; MarkDirty(); } }
    }

    /// <summary>相对于锚点的偏移位置。仅当锚点为单点（AnchorMin == AnchorMax）时有效。</summary>
    public Vector2 AnchoredPosition
    {
        get => _anchoredPosition;
        set { if (!_anchoredPosition.Equals(value)) { _anchoredPosition = value; MarkDirty(); } }
    }

    /// <summary>快速设置锚点预设。同时设置 AnchorMin/AnchorMax 和 Pivot。</summary>
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

    /// <summary>是否在某轴上拉伸（AnchorMin != AnchorMax）。</summary>
    public bool IsStretchX => Math.Abs(_anchorMin.X - _anchorMax.X) > 0.001f;
    public bool IsStretchY => Math.Abs(_anchorMin.Y - _anchorMax.Y) > 0.001f;

    /// <summary>计算锚点在父容器中的参考坐标（给定父容器宽高）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (float anchorX, float anchorY) GetAnchorPosition(float parentW, float parentH)
    {
        float ax = parentW * (IsStretchX ? (_anchorMin.X + _anchorMax.X) * 0.5f : _anchorMin.X);
        float ay = parentH * (IsStretchY ? (_anchorMin.Y + _anchorMax.Y) * 0.5f : _anchorMin.Y);
        return (ax, ay);
    }

    /// <summary>计算 WPF Canvas 定位坐标（元素左上角相对于 Canvas）。engine 内部调用。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal (double left, double top) ComputeCanvasPosition(double canvasW, double canvasH, double elemW, double elemH)
    {
        // 锚点在父容器中的位置
        float ax = _anchorMin.X * (float)canvasW;
        float ay = _anchorMin.Y * (float)canvasH;

        if (IsStretchX)
        {
            // 拉伸模式：left = anchorMin 位置 + anchoredPosition.x
            // width = (anchorMax - anchorMin) * parentW + sizeDelta.x
            ax += _anchoredPosition.X;
        }
        else
        {
            // 点锚模式：left = anchorX - pivot * elemW + anchoredPosition.x
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

/// <summary>锚点预设。14 种模式（参考 Iguina AnchorMode）。</summary>
public enum AnchorPreset
{

    #region 固定锚点（AnchorMin == AnchorMax）
    TopLeft, TopCenter, TopRight,
    MiddleLeft, MiddleCenter, MiddleRight,
    BottomLeft, BottomCenter, BottomRight,

    #endregion

    #region 拉伸模式
    /// <summary>拉伸铺满整个父容器。</summary>
    StretchFull,
    /// <summary>宽度拉伸，锚点顶部。</summary>
    StretchTop,
    /// <summary>宽度拉伸，锚点中部。</summary>
    StretchMiddle,
    /// <summary>宽度拉伸，锚点底部。</summary>
    StretchBottom,
    /// <summary>高度拉伸，锚点左侧。</summary>
    StretchLeft,
    /// <summary>高度拉伸，锚点居中。</summary>
    StretchCenter,
    /// <summary>高度拉伸，锚点右侧。</summary>
    StretchRight,
    #endregion
}
