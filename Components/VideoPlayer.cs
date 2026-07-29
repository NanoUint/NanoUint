using System.IO;

namespace NanoUint;

/// <summary>
/// 视频播放器组件。全屏播放视频。
/// </summary>
public sealed class VideoPlayer : Component
{
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;
    public event Action? OnFinished;

    /// <summary>播放指定路径的视频。</summary>
    public void Play(string resourcePath)
    {
        _isPlaying = true;

        // 从嵌入资源导出到临时文件（WPF MediaElement 需要文件路径或 URL）
        var tmpFile = ExtractToTemp(resourcePath);
        if (tmpFile == null)
        {
            _isPlaying = false;
            OnFinished?.Invoke();
            return;
        }

        Application.Host?.PlayVideo(tmpFile, () =>
        {
            _isPlaying = false;
            OnFinished?.Invoke();
        });
    }

    public void Stop()
    {
        Application.Host?.StopVideo();
        _isPlaying = false;
    }

    private static string? ExtractToTemp(string resourcePath)
    {
        var stream = AssetDatabase.OpenStream(resourcePath);
        if (stream == null) return null;

        var tmpDir = Path.Combine(Path.GetTempPath(), "NanoUint", "Video");
        Directory.CreateDirectory(tmpDir);
        var tmpFile = Path.Combine(tmpDir, Path.GetFileName(resourcePath));

        using (stream)
        using (var fs = File.Create(tmpFile))
            stream.CopyTo(fs);

        return tmpFile;
    }
}
