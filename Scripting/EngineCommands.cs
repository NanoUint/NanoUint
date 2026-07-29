using NanoUint.Drawing;

namespace NanoUint.Scripting;

/// <summary>
/// 引擎内置脚本命令。提供背景、立绘、音频、视频操作，以及对白/旁白。
/// 通过 [RegistryInScript] 自动注册到 ScriptEngine。
/// 直接操作 Scene 中对应名称的 GameObject，无 WPF 依赖。
///
/// .vns 用法:
///   @bg("Resources.Backgrounds.BG01A.png")
///   @sprite("Resources.Characters.kurisu_normal.png", pos: "center")
///   @bgm("Resources.Audio.bgm03.ogg")
///   @sfx("Resources.Audio.sgse001.ogg")
///   @voice("Resources.Audio.voice_kurisu_01.ogg")
///   @say("speaker", "text")          — 角色对白
///   @narration("text")               — 旁白
///   @movie("Resources.Movies.op.mp4")
/// </summary>
public class EngineCommands
{
    private static readonly string[] PositionNames = { "left", "center", "right", "offscreenleft", "offscreenright" };

    #region 对白 / 旁白

    /// <summary>@say("speaker", "text") — 显示角色对白并暂停脚本等待点击推进。</summary>
    [RegistryInScript("say")]
    public void Say(ScriptCommandContext ctx)
    {
        var speaker = ctx.Arg<string>(0) ?? "";
        var text = ctx.Arg<string>(1) ?? "";
        if (string.IsNullOrEmpty(text)) return;

        // 触发对白事件（ScriptManager → GameScreen.OnDialogue）
        ctx.Engine.RaiseText(speaker, text);
        ctx.Engine.RequestPause();
    }

    /// <summary>@narration("text") — 显示旁白文本并暂停脚本等待点击推进。</summary>
    [RegistryInScript("narration")]
    public void Narration(ScriptCommandContext ctx)
    {
        var text = ctx.Arg<string>(0) ?? "";
        if (string.IsNullOrEmpty(text)) return;

        ctx.Engine.RaiseText(null, text);
        ctx.Engine.RequestPause();
    }

    #endregion

    #region 背景

    [RegistryInScript("bg")]
    public void SetBackground(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        if (string.IsNullOrEmpty(path)) return;
        var go = SceneManager.GetActiveScene().FindObject("Background");
        if (go?.GetComponent<BackgroundRenderer>() is { } sr)
        {
            var sprite = AssetDatabase.Load<Sprite>(path);
            if (sprite != null) sr.Sprite = sprite;
            else Debug.LogWarning($"@bg: sprite not found: '{path}'");
        }
    }

    [RegistryInScript("bg_clear")]
    public void ClearBackground(ScriptCommandContext ctx)
    {
        var go = SceneManager.GetActiveScene().FindObject("Background");
        if (go?.GetComponent<BackgroundRenderer>() is { } sr)
            sr.Sprite = null;
    }

    #endregion

    #region 角色立绘

    [RegistryInScript("sprite")]
    public void ShowSprite(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        var posStr = ctx.Get<string>("pos") ?? "center";
        var opacity = ctx.Get<double>("opacity", 1.0);

        if (string.IsNullOrEmpty(path)) return;

        var go = SceneManager.GetActiveScene().FindObject("Character");
        if (go != null)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.Sprite = AssetDatabase.Load<Sprite>(path);

            go.Transform.Opacity = (float)Math.Clamp(opacity, 0.0, 1.0);
            go.Transform.X = PositionToFloat(posStr);
        }
    }

    [RegistryInScript("sprite_hide")]
    public void HideSprite(ScriptCommandContext ctx)
    {
        var go = SceneManager.GetActiveScene().FindObject("Character");
        if (go?.GetComponent<SpriteRenderer>() is { } sr)
            sr.Sprite = null;
    }

    [RegistryInScript("sprite_pos")]
    public void SetSpritePosition(ScriptCommandContext ctx)
    {
        var go = SceneManager.GetActiveScene().FindObject("Character");
        if (go != null)
            go.Transform.X = PositionToFloat(ctx.Arg<string>(0) ?? "center");
    }

    [RegistryInScript("sprite_opacity")]
    public void SetSpriteOpacity(ScriptCommandContext ctx)
    {
        var go = SceneManager.GetActiveScene().FindObject("Character");
        if (go != null)
            go.Transform.Opacity = (float)Math.Clamp(ctx.Arg<double>(0, 1.0), 0.0, 1.0);
    }

    private static float PositionToFloat(string pos) => pos.ToLowerInvariant() switch
    {
        "left" => 0.15f,
        "center" => 0.5f,
        "right" => 0.85f,
        "offscreenleft" => -0.2f,
        "offscreenright" => 1.2f,
        _ => 0.5f,
    };

    #endregion

    #region 音频 — 直接调用 AudioManager

    [RegistryInScript("bgm")]
    public void PlayBGM(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        if (!string.IsNullOrEmpty(path)) AudioManager.PlayBGM(path);
    }

    [RegistryInScript("bgm_stop")] public void StopBGM(ScriptCommandContext ctx) => AudioManager.StopBGM();
    [RegistryInScript("bgm_pause")] public void PauseBGM(ScriptCommandContext ctx) => AudioManager.PauseBGM();
    [RegistryInScript("bgm_resume")] public void ResumeBGM(ScriptCommandContext ctx) => AudioManager.ResumeBGM();

    [RegistryInScript("sfx")]
    public void PlaySFX(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        if (!string.IsNullOrEmpty(path)) AudioManager.PlaySFX(path);
    }

    [RegistryInScript("voice")]
    public void PlayVoice(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        if (string.IsNullOrEmpty(path)) return;
        AudioManager.PlayVoice(path);
        // 不暂停：让脚本继续到 @say，由 @say 暂停
        // Voice 会在 ScriptManager.Continue() 时自动停止
    }

    [RegistryInScript("volume")]
    public void SetVolume(ScriptCommandContext ctx)
    {
        AudioManager.MasterVolume = (float)Math.Clamp(ctx.Arg<double>(0, 0.8), 0, 1);
        AudioManager.BGMVolume = (float)Math.Clamp(ctx.Get<double>("bgm", 0.8), 0, 1);
        AudioManager.SFXVolume = (float)Math.Clamp(ctx.Get<double>("sfx", 0.8), 0, 1);
        AudioManager.VoiceVolume = (float)Math.Clamp(ctx.Get<double>("voice", 1.0), 0, 1);
    }

    [RegistryInScript("stop_audio")]
    public void StopAllAudio(ScriptCommandContext ctx) => AudioManager.StopAll();

    #endregion

    #region 视频

    [RegistryInScript("movie")]
    public void PlayMovie(ScriptCommandContext ctx)
    {
        var path = ctx.Arg<string>(0) ?? "";
        if (string.IsNullOrEmpty(path)) return;

        var go = SceneManager.GetActiveScene().FindObject("VideoPlayer");
        if (go?.GetComponent<VideoPlayer>() is { } vp)
        {
            vp.Play(path);
            ctx.Engine.RequestPause();
            vp.OnFinished += () => { if (ctx.Engine.IsRunning) ctx.Engine.Continue(); };
        }
        else
        {
            Debug.LogWarning("@movie: No VideoPlayer component found in scene.");
            ctx.Engine.RequestPause();
            ctx.Engine.Continue();
        }
    }

    #endregion
}
