namespace NanoUint.Scripting;

/// <summary>
/// Compiles a parsed .vns document (AST) into a flat sequence of ScriptSteps.
/// Resolves aliases, if/choice structures, and jump targets.
/// </summary>
public class VnsCompiler
{
    private readonly ScriptCommandRegistry _registry;
    private readonly Dictionary<string, string> _aliases;

    public VnsCompiler(ScriptCommandRegistry registry)
    {
        _registry = registry;
        _aliases = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Compile a document to executable script steps</summary>
    public CompiledScript Compile(VnsDocument document)
    {
        _aliases.Clear();

        // Process directives: register aliases
        foreach (var dir in document.Directives)
        {
            if (dir is AliasDirective alias)
                _aliases[alias.ShortName] = alias.TargetCommand;
        }

        var steps = new List<ScriptStep>();
        CompileBlocks(document.Blocks, steps);
        return new CompiledScript { Steps = steps, Aliases = new Dictionary<string, string>(_aliases) };
    }

    private void CompileBlocks(List<VnsBlock> blocks, List<ScriptStep> output, string? insideIfCondition = null)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case LabelBlock label:
                    output.Add(new ScriptStep { Type = ScriptStepType.Label, Label = label.Name, Location = label.Location });
                    break;

                case TextBlock text:
                    output.Add(new ScriptStep
                    {
                        Type = ScriptStepType.Text,
                        Text = text.Text,
                        Speaker = text.Speaker,
                        Location = text.Location
                    });
                    break;

                case CommandBlock cmd:
                    CompileCommand(cmd, output);
                    break;

                case IfBlock ifBlock:
                    CompileIf(ifBlock, output);
                    break;

                case ChoiceBlock choice:
                    CompileChoice(choice, output);
                    break;
            }
        }
    }

    private void CompileCommand(CommandBlock cmd, List<ScriptStep> output)
    {
        // Resolve alias
        var resolvedName = _aliases.TryGetValue(cmd.CommandName, out var target) ? target : cmd.CommandName;

        var step = new ScriptStep
        {
            Type = cmd.CommandName == "jump" ? ScriptStepType.Jump : ScriptStepType.Command,
            CommandName = resolvedName,
            Location = cmd.Location,
            Parameters = new Dictionary<string, object?>()
        };

        // Add positional args
        for (int i = 0; i < cmd.Arguments.Count; i++)
        {
            step.Parameters[$"_arg{i}"] = ConvertValue(cmd.Arguments[i].Value);
        }

        // Add named args
        foreach (var (key, val) in cmd.NamedArguments)
        {
            step.Parameters[key] = ConvertValue(val);
        }

        // Handle jump: the first positional arg is the target label
        if (cmd.CommandName == "jump" && cmd.Arguments.Count > 0 && cmd.Arguments[0].Value is VnsLabelRef lr)
        {
            step.Parameters["target"] = lr.LabelName;
        }

        output.Add(step);
    }

    private void CompileIf(IfBlock ifBlock, List<ScriptStep> output)
    {
        var condition = ifBlock.Condition ?? "";
        var ifBody = new List<ScriptStep>();
        CompileBlocks(ifBlock.Body, ifBody);

        var elifs = new List<(string Condition, List<ScriptStep> Body)>();
        foreach (var elif in ifBlock.ElseIfs)
        {
            var elifBody = new List<ScriptStep>();
            CompileBlocks(elif.Body, elifBody);
            elifs.Add((elif.Condition ?? "", elifBody));
        }

        List<ScriptStep>? elseBody = null;
        if (ifBlock.ElseBody != null)
        {
            elseBody = new List<ScriptStep>();
            CompileBlocks(ifBlock.ElseBody, elseBody);
        }

        output.Add(new ScriptStep
        {
            Type = ScriptStepType.If,
            Condition = condition,
            IfBody = ifBody,
            ElseIfs = elifs,
            ElseBody = elseBody,
            Location = ifBlock.Location
        });
    }

    private void CompileChoice(ChoiceBlock choice, List<ScriptStep> output)
    {
        output.Add(new ScriptStep
        {
            Type = ScriptStepType.Choice,
            Choices = choice.Options.Select(o => o.Text).ToList(),
            Condition = string.Join(",", choice.Options.Select(o => o.TargetLabel ?? "")),
            Location = choice.Location
        });
    }

    private static object? ConvertValue(VnsValue value)
    {
        return value switch
        {
            VnsString s => s.Value,
            VnsNumber n => n.Value,
            VnsBool b => b.Value,
            VnsNull => null,
            VnsColor c => c.Hex,
            VnsVector2 v => (v.X, v.Y),
            VnsLabelRef l => l.LabelName,
            VnsVariable v => $"{v.Scope}{v.Name}",
            VnsResourceRef r => $"&{r.ResourceType}(\"{r.Path}\")",
            _ => value.ToString()
        };
    }
}

/// <summary>The result of compiling a .vns script</summary>
public class CompiledScript
{
    public List<ScriptStep> Steps { get; init; } = new();
    public Dictionary<string, string> Aliases { get; init; } = new();

    /// <summary>Get all labels defined in this script</summary>
    public IEnumerable<string> Labels =>
        Steps.Where(s => s.Type == ScriptStepType.Label && s.Label != null)
             .Select(s => s.Label!);
}
