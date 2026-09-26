using System.Reflection;
using NanoUintVN.Dialogue;
using NanoUintVN.Save;

namespace NanoUintVN.Tests;

public class ArchitectureGuardTests
{
    [Fact]
    public void NanoUintVN_DoesNotReferenceSteinsGateX()
    {
        var assembly = typeof(VNDocument).Assembly;
        var references = assembly.GetReferencedAssemblies();
        var steinsGateXRef = references.FirstOrDefault(a =>
            a.Name?.Contains("SteinsGateX", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Null(steinsGateXRef);
    }

    [Fact]
    public void NanoUintVN_ReferencesNanoUint()
    {
        var assembly = typeof(VNDocument).Assembly;
        var references = assembly.GetReferencedAssemblies();
        var nanoUintRef = references.FirstOrDefault(a =>
            a.Name?.Equals("NanoUint", StringComparison.OrdinalIgnoreCase) == true);
        Assert.NotNull(nanoUintRef);
    }

    [Fact]
    public void DialoguePlayer_PublicApi_Surface()
    {
        var type = typeof(DialoguePlayer);
        var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).ToList();
        Assert.Contains("LoadDocument", publicMethods);
        Assert.Contains("Start", publicMethods);
        Assert.Contains("Advance", publicMethods);
        Assert.Contains("SelectChoice", publicMethods);
        Assert.Contains("Pause", publicMethods);
        Assert.Contains("Resume", publicMethods);
        Assert.Contains("Stop", publicMethods);
        Assert.Contains("SaveState", publicMethods);
        Assert.Contains("LoadState", publicMethods);
    }

    [Fact]
    public void SaveManagerV2_UsesInterfaces()
    {
        var type = typeof(SaveManagerV2);
        var ctor = type.GetConstructors().First();
        var pars = ctor.GetParameters();
        Assert.Equal("ISaveStore", pars[0].ParameterType.Name);
        Assert.Equal("ISaveBoundary", pars[1].ParameterType.Name);
        Assert.Equal("ISaveManifestProjector", pars[2].ParameterType.Name);
    }

    [Fact]
    public void PageTransition_IsRecordStruct()
    {
        var type = typeof(PageTransition);
        Assert.True(type.IsValueType);
        var props = type.GetProperties().Select(p => p.Name).ToList();
        Assert.Contains("Kind", props);
        Assert.Contains("Duration", props);
    }
}
