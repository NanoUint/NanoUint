using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NanoUint.Rendering;

/// <summary>Base class for IRenderNode backed by a WPF UIElement.</summary>
public abstract class WpfRenderNode : IRenderNode
{
    public UIElement Element { get; }

    protected WpfRenderNode(UIElement element)
    {
        Element = element;
    }

    public abstract void Sync(Component c);

    public virtual void SetTransform(in RenderTransform2D t)
    {
        if (Element is FrameworkElement fe)
        {
            fe.Opacity = t.Opacity;

            if (t.FlipX)
                fe.RenderTransform = new ScaleTransform(-1, 1, fe.ActualWidth > 0 ? fe.ActualWidth / 2 : 100, 0);
            else
                fe.RenderTransform = System.Windows.Media.Transform.Identity;
        }
    }

    public virtual void SetSorting(SortingKey key)
    {
        // Use OrderInLayer for WPF ZIndex; SortingLayer is conceptual for now
        Canvas.SetZIndex(Element, key.OrderInLayer);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) { }
}
