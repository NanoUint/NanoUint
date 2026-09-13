namespace NanoUint;

#pragma warning disable CS0618 // Wrappers delegate to obsolete static APIs during migration

/// <summary>Container for all engine runtime services.</summary>
public sealed class EngineContext
{
    public static EngineContext? Default { get; internal set; }

    public CoroutineScheduler Coroutines { get; }
    public AudioServiceWrapper Audio { get; }
    public SaveServiceWrapper Save { get; }
    public InputServiceWrapper Input { get; }

    public EngineContext()
    {
        Coroutines = CoroutineScheduler.Instance;
        Audio = new AudioServiceWrapper();
        Save = new SaveServiceWrapper();
        Input = new InputServiceWrapper();
    }
}

public sealed class AudioServiceWrapper
{
    public float MasterVolume { get => AudioManager.MasterVolume; set => AudioManager.MasterVolume = value; }
    public float BGMVolume { get => AudioManager.BGMVolume; set => AudioManager.BGMVolume = value; }
    public float SFXVolume { get => AudioManager.SFXVolume; set => AudioManager.SFXVolume = value; }
    public float VoiceVolume { get => AudioManager.VoiceVolume; set => AudioManager.VoiceVolume = value; }
    public bool IsVoicePlaying => AudioManager.IsVoicePlaying;
    public double VoiceDuration => AudioManager.VoiceDuration;
    public event Action? VoiceFinished { add => AudioManager.VoiceFinished += value; remove => AudioManager.VoiceFinished -= value; }
    public void PlayBGM(string path) => AudioManager.PlayBGM(path);
    public void StopBGM() => AudioManager.StopBGM();
    public void PauseBGM() => AudioManager.PauseBGM();
    public void ResumeBGM() => AudioManager.ResumeBGM();
    public void PlaySFX(string path) => AudioManager.PlaySFX(path);
    public void PlayVoice(string path) => AudioManager.PlayVoice(path);
    public void StopVoice() => AudioManager.StopVoice();
    public void StopAll() => AudioManager.StopAll();
}

public sealed class SaveServiceWrapper
{
    public const int MaxSlots = SaveManager.MaxSlots;
    public const int QuickSaveSlot = SaveManager.QuickSaveSlot;
    public bool AllowSave { get => SaveManager.AllowSave; set => SaveManager.AllowSave = value; }
    public bool QuickSaveExists => SaveManager.QuickSaveExists;
    public void Save(int slot, SaveData data) => SaveManager.Save(slot, data);
    public SaveData? Load(int slot) => SaveManager.Load(slot);
    public void Delete(int slot) => SaveManager.Delete(slot);
    public bool SlotExists(int slot) => SaveManager.SlotExists(slot);
    public List<SaveSlotInfo> GetAllSlots() => SaveManager.GetAllSlots();
    public int GetSlotCount() => SaveManager.GetSlotCount();
    public void QuickSave(SaveData data) => SaveManager.QuickSave(data);
    public SaveData? QuickLoad() => SaveManager.QuickLoad();
    public SaveSlotInfo? GetQuickSaveInfo() => SaveManager.GetQuickSaveInfo();
}

public sealed class InputServiceWrapper
{
    public Func<EngineKey, bool>? KeyDispatch { get => InputManager.KeyDispatch; set => InputManager.KeyDispatch = value; }
    public bool IsAdvancePressedThisFrame() => InputManager.IsAdvancePressedThisFrame();
    public bool IsSkipHeld() => InputManager.IsSkipHeld();
    public bool IsAutoModeToggledThisFrame() => InputManager.IsAutoModeToggledThisFrame();
    public bool IsMenuPressedThisFrame() => InputManager.IsMenuPressedThisFrame();
    public void ConsumeAdvancePress() => InputManager.ConsumeAdvancePress();
    public void ConsumeUpPress() => InputManager.ConsumeUpPress();
    public void ConsumeDownPress() => InputManager.ConsumeDownPress();
    public void ConsumeLeftPress() => InputManager.ConsumeLeftPress();
    public void ConsumeRightPress() => InputManager.ConsumeRightPress();
    public event Action? QuickSaveRequested { add => InputManager.QuickSaveRequested += value; remove => InputManager.QuickSaveRequested -= value; }
    public event Action? QuickLoadRequested { add => InputManager.QuickLoadRequested += value; remove => InputManager.QuickLoadRequested -= value; }
    public event Action? SystemMenuRequested { add => InputManager.SystemMenuRequested += value; remove => InputManager.SystemMenuRequested -= value; }
    public event Action? BacklogRequested { add => InputManager.BacklogRequested += value; remove => InputManager.BacklogRequested -= value; }
    public event Action? HideUIToggleRequested { add => InputManager.HideUIToggleRequested += value; remove => InputManager.HideUIToggleRequested -= value; }
    public event Action? SaveScreenRequested { add => InputManager.SaveScreenRequested += value; remove => InputManager.SaveScreenRequested -= value; }
    public event Action? LoadScreenRequested { add => InputManager.LoadScreenRequested += value; remove => InputManager.LoadScreenRequested -= value; }
    public event Action? SkipToggleRequested { add => InputManager.SkipToggleRequested += value; remove => InputManager.SkipToggleRequested -= value; }
    public event Action? AutoToggleRequested { add => InputManager.AutoToggleRequested += value; remove => InputManager.AutoToggleRequested -= value; }
    public event Action? PhoneToggleRequested { add => InputManager.PhoneToggleRequested += value; remove => InputManager.PhoneToggleRequested -= value; }
    public void FireQuickSave() => InputManager.FireQuickSave();
    public void FireQuickLoad() => InputManager.FireQuickLoad();
    public void FireSystemMenu() => InputManager.FireSystemMenu();
    public void FireBacklog() => InputManager.FireBacklog();
    public void FireHideUIToggle() => InputManager.FireHideUIToggle();
    public void FireSaveScreen() => InputManager.FireSaveScreen();
    public void FireLoadScreen() => InputManager.FireLoadScreen();
    public void FireSkipToggle() => InputManager.FireSkipToggle();
    public void FireAutoToggle() => InputManager.FireAutoToggle();
    public void FirePhoneToggle() => InputManager.FirePhoneToggle();
}
