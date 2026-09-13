#pragma warning disable CS0618 // Obsolete - NanoUint internal consumers still use these components
namespace NanoUint;

public sealed class WaitForChoice
{
    public ChoiceGroup ChoiceGroup { get; }
    public WaitForChoice(ChoiceGroup group) => ChoiceGroup = group;
}
