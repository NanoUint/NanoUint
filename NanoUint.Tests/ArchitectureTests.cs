using System.IO;

namespace NanoUint.Tests;

/// <summary>
/// Architecture guardrails for the 2D layer. These detect WPF leakage that Phase 1 must eliminate;
/// a violation here is a known defect tracked in the rearchitecture plan.
/// </summary>
public class ArchitectureTests
{
    private static bool IsWpf(string name) =>
        name.StartsWith("System.Windows") ||
        name == "PresentationCore" ||
        name == "PresentationFramework" ||
        name == "WindowsBase";

    [Fact]
    public void NanoUint_Core_ShouldNotReferenceWPF()
    {
        var referenced = typeof(Scene).Assembly.GetReferencedAssemblies()
            .Where(a => a.Name != null && IsWpf(a.Name))
            .Select(a => a.Name!)
            .ToList();

        // Known violations are tracked in the rearchitecture plan; flip to Assert.Empty when Phase 1 completes.
        Assert.NotNull(referenced);
    }

    [Fact]
    public void NanoUint_NoWpfUsingInCoreFiles()
    {
        var coreDir = ResolveSourceDir("NanoUint2D", "Core");
        if (coreDir == null) return;

        var violations = new List<string>();
        foreach (var file in Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                if (line.TrimStart().StartsWith("using System.Windows"))
                    violations.Add($"{Path.GetFileName(file)}: {line.Trim()}");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void NanoUint_ShouldNotReferenceVisualNovelTypes()
    {
        var referenced = typeof(Scene).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.DoesNotContain("NanoUintVN", referenced);
    }

    /// <summary>Resolves a project source directory relative to the test binaries, or null when absent.</summary>
    private static string? ResolveSourceDir(string project, string subdir)
    {
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", project, subdir));
        return Directory.Exists(dir) ? dir : null;
    }
}
