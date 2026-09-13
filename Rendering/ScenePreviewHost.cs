using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NanoUint.Rendering;

/// <summary>Scene preview host for editor play mode. Embeds engine rendering into any Canvas.</summary>
public sealed class ScenePreviewHost
{
    private readonly WpfRenderer _renderer;
    private DateTime _lastFrame;
    private bool _running;

    /// <summary>Render target Canvas.</summary>
    public Canvas Root { get; }

    /// <summary>Preview scene; the editor adds GameObjects/components through it.</summary>
    public Scene Scene { get; }

    /// <param name="referenceWidth">Logical canvas width (editor default 1920).</param>
    /// <param name="referenceHeight">Logical canvas height (editor default 1080).</param>
    public ScenePreviewHost(Canvas canvas, double referenceWidth = 1920, double referenceHeight = 1080)
    {
        Root = canvas;
        Scene = new Scene("Preview");
        _renderer = new WpfRenderer(canvas);
        _renderer.SetActiveScene(Scene);
        SceneManager.LoadScene(Scene);

        // Logical canvas scaling: map actual Canvas size to normalized 1920×1080 coordinates
        var scalerGo = Scene.AddObject("__CanvasScaler");
        var scaler = scalerGo.AddComponent<CanvasScaler>();
        scaler.ReferenceWidth = (float)referenceWidth;
        scaler.ReferenceHeight = (float)referenceHeight;
        scaler.ScaleMode = CanvasScaleMode.ScaleWithScreenSize;
    }

    /// <summary>Starts the frame loop.</summary>
    public void Start()
    {
        if (_running) return;
        _running = true;
        _lastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += OnFrame;
    }

    /// <summary>Stops the frame loop; called when the window closes.</summary>
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    /// <summary>Runs Start on all root objects.</summary>
    public void StartScene()
    {
        foreach (var go in Scene.RootObjects.ToList())
        {
            if (go.Name != "__CanvasScaler") go.NotifyStart();
        }
    }

    /// <summary>Clears scene objects, keeping the CanvasScaler.</summary>
    public void Clear()
    {
        foreach (var go in Scene.RootObjects.ToList())
        {
            if (go.Name != "__CanvasScaler") go.Destroy();
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (!_running) return;
        var now = DateTime.UtcNow;
        var dt = Math.Min((float)(now - _lastFrame).TotalSeconds, 0.1f);
        _lastFrame = now;
        try
        {
            Scene.Update(dt);
            _renderer.UpdateDirtyComponents();
            InputManager.EndFrame();
        }
        catch (Exception ex)
        {
            Diagnostics.Logger.Error("Preview", "Frame exception", ex);
        }
    }
}
