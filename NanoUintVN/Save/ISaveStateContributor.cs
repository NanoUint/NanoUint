namespace NanoUintVN.Save;

/// <summary>Components that contribute state to a save file.</summary>
public interface ISaveStateContributor
{
    string Domain { get; }
    int SchemaVersion { get; }
    Type CapturedType { get; }
    object CaptureState();
    void RestoreState(object state);
}
