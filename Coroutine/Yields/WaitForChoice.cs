namespace NanoUint;

public sealed class WaitForChoice
{
    public ChoiceGroup ChoiceGroup { get; }
    public WaitForChoice(ChoiceGroup group) => ChoiceGroup = group;
}
