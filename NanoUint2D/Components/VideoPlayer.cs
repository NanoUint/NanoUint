using System.IO;

namespace NanoUint;

/// <summary>Video player component. Plays a video full-screen.</summary>
public sealed class VideoPlayer : Component
{
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;
    public event Action? OnFinished;

    /// <summary>Plays the video at the given path.</summary>
    public void Play(string resourcePath)
    {
        _isPlaying = true;

        // Extract the embedded resource to a temp file
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
