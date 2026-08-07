using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NanoUint.Rendering;

/// <summary>场景预览宿主（编辑器播放模式用）。把引擎渲染嵌入任意 Canvas。</summary>
public sealed class ScenePreviewHost
{
    private readonly WpfRenderer _renderer;
    private DateTime _lastFrame;
    private bool _running;

    /// <summary>渲染目标 Canvas。</summary>
    public Canvas Root { get; }

    /// <summary>预览场景（编辑器通过它添加 GameObject/组件）。</summary>
    public Scene Scene { get; }

    /// <param name="referenceWidth">逻辑画布宽度（编辑器默认 1920）。</param>
    /// <param name="referenceHeight">逻辑画布高度（编辑器默认 1080）。</param>
    public ScenePreviewHost(Canvas canvas, double referenceWidth = 1920, double referenceHeight = 1080)
    {
        Root = canvas;
        Scene = new Scene("Preview");
        _renderer = new WpfRenderer(canvas);
        _renderer.SetActiveScene(Scene);
        SceneManager.LoadScene(Scene);

        // 逻辑画布缩放：Canvas 实际尺寸 → 1920×1080 归一化坐标
        var scalerGo = Scene.AddObject("__CanvasScaler");
        var scaler = scalerGo.AddComponent<CanvasScaler>();
        scaler.ReferenceWidth = (float)referenceWidth;
        scaler.ReferenceHeight = (float)referenceHeight;
        scaler.ScaleMode = CanvasScaleMode.ScaleWithScreenSize;
    }

    /// <summary>启动帧循环。</summary>
    public void Start()
    {
        if (_running) return;
        _running = true;
        _lastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += OnFrame;
    }

    /// <summary>停止帧循环（窗口关闭时调用）。</summary>
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    /// <summary>触发所有根对象的 Start（编辑器构建完成后调用；Awake 已由 AddObject 触发）。</summary>
    public void StartScene()
    {
        foreach (var go in Scene.RootObjects.ToList())
        {
            if (go.Name != "__CanvasScaler") go.NotifyStart();
        }
    }

    /// <summary>清空场景对象（保留 CanvasScaler）。</summary>
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
