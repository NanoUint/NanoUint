using NanoUintVN.Dialogue;
using NanoUintVN.Save;
using NanoUintVN.Settings;

namespace NanoUintVN;

/// <summary>Configuration for auto-save behavior.</summary>
public sealed class AutoSaveConfig
{
    public bool Enabled { get; set; }
    public int MaxSlots { get; set; } = 3;
    public int NextSlot { get; set; }
}

/// <summary>Manages auto-advance, skip mode, and auto-save for VN playback.</summary>
public sealed class VNPlaybackController
{
    private readonly DialoguePlayer _player;
    private readonly SaveManagerV2? _saveManager;

    public bool IsAutoAdvance { get; private set; }
    public float AutoAdvanceDelay { get; set; } = 3f;
    public bool IsSkipMode { get; private set; }
    public AutoSaveConfig AutoSave { get; } = new();

    /// <summary>Skip policy. Enforcing ReadOnly needs a read-history source, which is not wired yet.</summary>
    public SkipMode SkipPolicy { get; set; } = SkipMode.ReadOnly;

    /// <summary>Automatic quick save policy, honoured at the boundaries the runtime reports.</summary>
    public AutoQuickSaveMode AutoQuickSavePolicy { get; set; } = AutoQuickSaveMode.Off;

    private float _autoAdvanceTimer;

    public VNPlaybackController(DialoguePlayer player, SaveManagerV2? saveManager = null)
    {
        _player = player;
        _saveManager = saveManager;
    }

    public void ToggleAutoAdvance()
    {
        IsAutoAdvance = !IsAutoAdvance;
        IsSkipMode = false;
        _autoAdvanceTimer = 0f;
    }

    public void ToggleSkip()
    {
        IsSkipMode = !IsSkipMode;
        IsAutoAdvance = false;
    }

    public void SetAutoAdvance(bool enabled)
    {
        IsAutoAdvance = enabled;
        _autoAdvanceTimer = 0f;
    }

    public void SetSkip(bool enabled)
    {
        IsSkipMode = enabled;
    }

    public void Update(float deltaTime)
    {
        if (IsSkipMode && _player.State == PlayerState.WaitingForAdvance)
        {
            _player.Advance();
            return;
        }

        if (IsAutoAdvance && _player.State == PlayerState.WaitingForAdvance)
        {
            _autoAdvanceTimer += deltaTime;
            if (_autoAdvanceTimer >= AutoAdvanceDelay)
            {
                _autoAdvanceTimer = 0f;
                _player.Advance();
            }
        }
        else
        {
            _autoAdvanceTimer = 0f;
        }

        if (_player.State == PlayerState.Playing)
        {
            var beat = _player.CurrentBeat;
            if (beat != null)
                _player.UpdateTypewriter(deltaTime, beat.TextSpeed);
        }
    }

    public void TryAutoSave()
    {
        if (_saveManager == null || !AutoSave.Enabled) return;

        var slotId = AutoSave.NextSlot.ToString();
        var state = new VNSaveState
        {
            DocumentId = "", // set by caller
            PageIndex = _player.PageIndex,
            BeatIndex = _player.BeatIndex,
            Description = "Auto Save"
        };
        _saveManager.Save(slotId, state);
        AutoSave.NextSlot = (AutoSave.NextSlot + 1) % AutoSave.MaxSlots;
    }
}
