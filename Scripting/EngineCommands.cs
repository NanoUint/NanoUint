using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using NanoUint.Services;

namespace NanoUint.Scripting;

/// <summary>
/// 引擎层内置的 [RegistryInScript] 命令。
/// 提供图片（背景、立绘）、音频（BGM、SFX、语音）和视频操作。
/// 这些命令由 NanoUint 引擎直接提供，不依赖具体游戏项目。
///
/// 在 .vns 中的用法：
///   @bg("school_day.png")
///   @sprite("rei_normal.png", pos: "center")
///   @bgm("theme_sad.mp3")
///   @sfx("click.wav")
///   @movie("op.mp4")
/// </summary>
public class EngineCommands
{
    private readonly AudioService _audio;
    private readonly IGameDisplay _display;
    private readonly Action<string>? _playMovie;
    private readonly string _bgAssetPath = Path.Combine("Assets", "Backgrounds");
    private readonly string _charAssetPath = Path.Combine("Assets", "Characters");

    /// <param name="playMovie">视频播放回调，传入视频完整路径</param>
    public EngineCommands(AudioService audio, IGameDisplay display, Action<string>? playMovie = null)
    {
        _audio = audio;
        _display = display;
        _playMovie = playMovie;
    }

    #region 背景

    /// <summary>@bg("filename.png") — 设置背景图片</summary>
    [RegistryInScript("bg")]
    public void SetBackground(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        if (string.IsNullOrEmpty(path)) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var fullPath = Path.Combine(_bgAssetPath, path);
            if (File.Exists(fullPath))
                _display.SetBackground(Path.GetFullPath(fullPath));
        });
    }

    /// <summary>@bg_clear() — 移除背景图片</summary>
    [RegistryInScript("bg_clear")]
    public void ClearBackground(ScriptCommandContext ctx)
    {
        Application.Current.Dispatcher.Invoke(() => _display.ClearBackground());
    }

    #endregion

    #region 角色立绘

    /// <summary>@sprite("filename.png", pos: "center", opacity: 1.0) — 显示角色立绘</summary>
    [RegistryInScript("sprite")]
    public void ShowSprite(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        var posStr = ctx.Get<string>("pos") ?? "center";
        var opacity = ctx.Get<double>("opacity", 1.0);

        if (string.IsNullOrEmpty(path)) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var fullPath = Path.Combine(_charAssetPath, path);
            if (File.Exists(fullPath))
                _display.SetSprite(Path.GetFullPath(fullPath), posStr, Math.Clamp(opacity, 0.0, 1.0));
        });
    }

    /// <summary>@sprite_hide() — 隐藏角色立绘</summary>
    [RegistryInScript("sprite_hide")]
    public void HideSprite(ScriptCommandContext ctx)
    {
        Application.Current.Dispatcher.Invoke(() => _display.ClearSprite());
    }

    /// <summary>@sprite_pos("center") — 更改角色位置</summary>
    [RegistryInScript("sprite_pos")]
    public void SetSpritePosition(ScriptCommandContext ctx)
    {
        Application.Current.Dispatcher.Invoke(() =>
            _display.SetSpritePosition(ctx.Arg<string>(0) ?? "center"));
    }

    /// <summary>@sprite_opacity(0.5) — 设置角色透明度</summary>
    [RegistryInScript("sprite_opacity")]
    public void SetSpriteOpacity(ScriptCommandContext ctx)
    {
        Application.Current.Dispatcher.Invoke(() =>
            _display.SetSpriteOpacity(Math.Clamp(ctx.Arg<double>(0, 1.0), 0.0, 1.0)));
    }

    #endregion

    #region 音频：BGM

    /// <summary>@bgm("filename.ext") — 播放循环背景音乐</summary>
    [RegistryInScript("bgm")]
    public void PlayBGM(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        if (!string.IsNullOrEmpty(path)) _audio.PlayBGM(path);
    }

    [RegistryInScript("bgm_stop")] public void StopBGM(ScriptCommandContext ctx) => _audio.StopBGM();
    [RegistryInScript("bgm_pause")] public void PauseBGM(ScriptCommandContext ctx) => _audio.PauseBGM();
    [RegistryInScript("bgm_resume")] public void ResumeBGM(ScriptCommandContext ctx) => _audio.ResumeBGM();

    #endregion

    #region 音频：SFX

    /// <summary>@sfx("filename.ext") — 播放一次性音效</summary>
    [RegistryInScript("sfx")]
    public void PlaySFX(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        if (!string.IsNullOrEmpty(path)) _audio.PlaySFX(path);
    }

    #endregion

    #region 音频：语音

    /// <summary>@voice("filename.ext") — 播放语音台词，脚本暂停等待语音结束</summary>
    [RegistryInScript("voice")]
    public void PlayVoice(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        if (string.IsNullOrEmpty(path)) return;

        _audio.PlayVoice(path);
        ctx.Engine.RequestPause();

        _audio.VoiceFinished += OnVoiceDone;
        void OnVoiceDone()
        {
            _audio.VoiceFinished -= OnVoiceDone;
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (ctx.Engine.IsRunning) ctx.Engine.Continue();
            });
        }
    }

    #endregion

    #region 音频：音量

    /// <summary>@volume(master, bgm, sfx, voice) — 设置所有音频音量 (0.0~1.0)</summary>
    [RegistryInScript("volume")]
    public void SetVolume(ScriptCommandContext ctx)
    {
        _audio.SetVolumes(
            Math.Clamp(ctx.Arg<double>(0, 0.8), 0, 1),
            Math.Clamp(ctx.Get<double>("bgm", 0.8), 0, 1),
            Math.Clamp(ctx.Get<double>("sfx", 0.8), 0, 1),
            Math.Clamp(ctx.Get<double>("voice", 1.0), 0, 1));
    }

    /// <summary>@stop_audio() — 停止所有音频</summary>
    [RegistryInScript("stop_audio")]
    public void StopAllAudio(ScriptCommandContext ctx) => _audio.StopAll();

    #endregion

    #region 视频

    /// <summary>@movie("filename.mp4") — 全屏播放视频（1280×720）</summary>
    [RegistryInScript("movie")]
    public void PlayMovie(ScriptCommandContext ctx)
    {
        if (_playMovie == null) return;
        var path = ctx.Arg<string>(0) ?? "";
        if (string.IsNullOrEmpty(path)) return;

        var fullPath = Path.Combine("Assets", "Movies", path);
        if (!File.Exists(fullPath)) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _playMovie(fullPath);
            ctx.Engine.Continue();
        });
        ctx.Engine.RequestPause();
    }

    #endregion
}
