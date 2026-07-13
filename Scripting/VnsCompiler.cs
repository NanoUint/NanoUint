namespace NanoUint.Scripting;

/// <summary>
/// 将解析后的 .vns 文档（AST）编译为扁平的 ScriptStep 序列。
/// 解析别名、if/choice 结构和跳转目标。
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

    /// <summary>将文档编译为可执行的脚本步骤</summary>
    public CompiledScript Compile(VnsDocument document)
    {
        _aliases.Clear();

        // 处理指令：注册别名
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
        // 解析别名
        var resolvedName = _aliases.TryGetValue(cmd.CommandName, out var target) ? target : cmd.CommandName;

        var step = new ScriptStep
        {
            Type = cmd.CommandName == "jump" ? ScriptStepType.Jump : ScriptStepType.Command,
            CommandName = resolvedName,
            Location = cmd.Location,
            Parameters = new Dictionary<string, object?>()
        };

        // 添加位置参数
        for (int i = 0; i < cmd.Arguments.Count; i++)
        {
            step.Parameters[$"_arg{i}"] = ConvertValue(cmd.Arguments[i].Value);
        }

        // 添加命名参数
        foreach (var (key, val) in cmd.NamedArguments)
        {
            step.Parameters[key] = ConvertValue(val);
        }

        // 处理跳转：第一个位置参数是目标标签
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

/// <summary>编译 .vns 脚本的结果</summary>
public class CompiledScript
{
    public List<ScriptStep> Steps { get; init; } = new();
    public Dictionary<string, string> Aliases { get; init; } = new();
    public string FilePath { get; set; } = "";

    /// <summary>获取此脚本中定义的所有标签</summary>
    public IEnumerable<string> Labels =>
        Steps.Where(s => s.Type == ScriptStepType.Label && s.Label != null)
             .Select(s => s.Label!);
}
