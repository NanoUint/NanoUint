namespace NanoUint;

/// <summary>音频源组件。挂载到 GameObject 上以播放 BGM/SFX/语音。</summary>
public sealed class AudioSource : Component
{
    private AudioClip? _clip;

    /// <summary>要播放的音频资源。</summary>
    public AudioClip? Clip
    {
        get => _clip;
        set { _clip = value; }
    }

    /// <summary>是否循环播放（BGM 用）。</summary>
    public bool IsLooping { get; set; }

    /// <summary>音量（0~1）。</summary>
    public float Volume { get; set; } = 1f;

    /// <summary>开始播放。</summary>
    public void Play()
    {
        if (_clip == null)
        {
            Debug.LogWarning("AudioSource.Play: Clip is null.");
            return;
        }
        if (IsLooping)
            AudioManager.PlayBGM(_clip.Path);
        else
            AudioManager.PlaySFX(_clip.Path);
    }

    /// <summary>停止播放。</summary>
    public void Stop()
    {
        AudioManager.StopBGM();
    }

    /// <summary>暂停。</summary>
    public void Pause()
    {
        AudioManager.PauseBGM();
    }
}
