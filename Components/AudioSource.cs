namespace NanoUint;

/// <summary>Plays BGM, SFX and voice audio.</summary>
public sealed class AudioSource : Component
{
    private AudioClip? _clip;

    /// <summary>Audio clip to play.</summary>
    public AudioClip? Clip
    {
        get => _clip;
        set { _clip = value; }
    }

    /// <summary>Whether to loop playback (used for BGM).</summary>
    public bool IsLooping { get; set; }

    /// <summary>Volume (0-1).</summary>
    public float Volume { get; set; } = 1f;

    /// <summary>Starts playback.</summary>
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

    /// <summary>Stops playback.</summary>
    public void Stop()
    {
        AudioManager.StopBGM();
    }

    /// <summary>Pauses playback.</summary>
    public void Pause()
    {
        AudioManager.PauseBGM();
    }
}
