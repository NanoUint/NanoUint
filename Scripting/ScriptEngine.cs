using System.IO;
using System.Reflection;
using NanoUint.Diagnostics;

namespace NanoUint.Scripting;

/// <summary>顶层脚本引擎。整合 .vns 脚本的词法分析、解析、编译和执行。</summary>
public class ScriptEngine
{
    private readonly ScriptCommandRegistry _registry;
    private readonly Dictionary<string, CompiledScript> _loadedScripts = new();
    private CompiledScript? _currentScript;
    private int _stepIndex;
    private bool _isRunning;
    private bool _paused;

    // 执行状态
    private readonly Dictionary<string, object?> _variables = new();
    private readonly Dictionary<string, bool> _flags = new();
    private readonly Stack<(CompiledScript Script, int StepIndex)> _callStack = new();
    /// <summary>当需要显示文本行时触发（旁白或对白）</summary>
    public event Action<string?, string>? OnText;

    /// <summary>当需要向玩家展示选项时触发</summary>
    public event Action<List<string>, List<string>>? OnChoice;

    /// <summary>当任意命令执行时触发</summary>
    public event Action<string, Dictionary<string, object?>>? OnCommand;

    /// <summary>当脚本结束或跳转到场景时触发</summary>
    public event Action? OnScriptEnd;

    /// <summary>从外部命令触发文本显示（用于 @say/@narration 等命令）。</summary>
    public void RaiseText(string? speaker, string text) => OnText?.Invoke(speaker, text);

    /// <summary>当前脚本步骤，如果未运行则为 null</summary>
    public ScriptStep? CurrentStep => _isRunning && _stepIndex < (_currentScript?.Steps.Count ?? 0)
        ? _currentScript!.Steps[_stepIndex] : null;

    public ScriptCommandRegistry Registry => _registry;
    public bool IsRunning => _isRunning;

    public ScriptEngine()
    {
        _registry = new ScriptCommandRegistry();
    }

    /// <summary>从游戏项目注册一个命令实现</summary>
    public void RegisterCommand(IScriptCommand command) => _registry.Register(command);

    /// <summary>扫描程序集中 [RegistryInScript] 装饰的静态方法并自动注册。</summary>
    public void ScanCommands(Assembly assembly) =>
        ScriptCommandScanner.ScanAssembly(this, assembly);

    /// <summary>扫描对象实例中 [RegistryInScript] 装饰的方法。</summary>
    public void ScanCommands(params object[] instances) =>
        ScriptCommandScanner.ScanInstances(this, instances);

    /// <summary>从文件路径加载 .vns 脚本</summary>
    public CompiledScript LoadFromFile(string filePath)
    {
        LogEngine($"LoadFromFile: reading {filePath}");
        var source = File.ReadAllText(filePath);
        LogEngine($"LoadFromFile: read {source.Length} chars");
        return LoadFromSource(source, filePath);
    }

    /// <summary>从字符串加载 .vns 脚本</summary>
    public CompiledScript LoadFromSource(string source, string filePath = "")
    {
        LogEngine($"LoadFromSource: creating lexer...");
        var lexer = new VnsLexer(source, filePath);
        LogEngine($"LoadFromSource: tokenizing...");
        var tokens = lexer.Tokenize();
        LogEngine($"LoadFromSource: got {tokens.Count} tokens");

        LogEngine($"LoadFromSource: parsing...");
        var parser = new VnsParser(tokens, filePath);
        var document = parser.Parse();
        LogEngine($"LoadFromSource: parsed {document.Blocks.Count} blocks, {parser.Errors.Count} errors");
        foreach (var err in parser.Errors)
            LogEngine($"  Parse error: {err}");

        LogEngine($"LoadFromSource: compiling...");
        var compiler = new VnsCompiler(_registry);
        var compiled = compiler.Compile(document);
        compiled.FilePath = filePath;
        LogEngine($"LoadFromSource: compiled {compiled.Steps.Count} steps");

        _loadedScripts[filePath] = compiled;
        return compiled;
    }

    /// <summary>开始执行已编译的脚本</summary>
    public void Run(CompiledScript script, string? startLabel = null)
    {
        _currentScript = script;
        _stepIndex = 0;
        _isRunning = true;
        _callStack.Clear();

        // 如果指定了起始标签，跳转到它
        if (startLabel != null)
        {
            var idx = FindLabel(startLabel);
            if (idx >= 0) _stepIndex = idx;
        }
        else
        {
            // 如果存在 #start 标签，跳转到那里
            var startIdx = FindLabel("start");
            if (startIdx >= 0) _stepIndex = startIdx;
        }

        LogEngine($"Run: _stepIndex={_stepIndex}, steps={_currentScript.Steps.Count}, dispatching ExecuteNext...");

        // 通过 Dispatcher 推迟第一步
        var disp = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        LogEngine($"Run: dispatcher ok, posting...");
        disp.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            () => {
                LogEngine($"Run: BeginInvoke fired, calling ExecuteNext");
                ExecuteNext();
                LogEngine($"Run: ExecuteNext returned");
            });
        LogEngine($"Run: BeginInvoke posted, Run returning");
    }

    /// <summary>请求引擎在当前命令完成后暂停</summary>
    public void RequestPause() => _paused = true;

    /// <summary>继续执行（在玩家跳过文本/选择后调用）</summary>
    public void Continue()
    {
        if (!_isRunning || _currentScript == null) return;

        // 推进到下一行时停止当前语音
        AudioManager.StopVoice();

        _paused = false;
        _stepIndex++;
        if (_stepIndex >= _currentScript.Steps.Count)
        {
            EndScript();
            return;
        }
        ExecuteNext();
    }

    /// <summary>根据索引选择选项（从0开始）</summary>
    public void SelectChoice(int choiceIndex)
    {
        if (CurrentStep?.Type != ScriptStepType.Choice) return;

        var labels = CurrentStep.Condition?.Split(',') ?? Array.Empty<string>();
        if (choiceIndex >= 0 && choiceIndex < labels.Length && !string.IsNullOrEmpty(labels[choiceIndex]))
        {
            JumpToLabel(labels[choiceIndex]);
        }
        else
        {
            Continue();
        }
    }

    /// <summary>跳转到当前脚本中的命名标签</summary>
    public void JumpToLabel(string labelName)
    {
        var idx = FindLabel(labelName);
        if (idx >= 0)
        {
            _stepIndex = idx;
            ExecuteNext();
        }
    }

    /// <summary>停止执行</summary>
    public void Stop()
    {
        _isRunning = false;
        _currentScript = null;
    }

    private static void LogEngine(string msg) => Logger.Trace("ScriptEngine", msg);

    // ---- 内部执行 ----

    /// <summary>处理一个交互步骤，自动跳过标签和非暂停命令，遇到玩家必须响应的事件时返回。</summary>
    private void ExecuteNext()
    {
        LogEngine($"ExecuteNext: _isRunning={_isRunning}, _stepIndex={_stepIndex}, steps={_currentScript?.Steps.Count}");
        if (!_isRunning || _currentScript == null) { LogEngine("ExecuteNext: abort - not running"); return; }

        // 跳过标签和非暂停命令，直到遇到交互内容或运行完所有步骤。
        int loopCount = 0;
        while (_stepIndex < _currentScript.Steps.Count)
        {
            if (++loopCount > 100)
            {
                Logger.Error("ScriptEngine",
                    $"ExecuteNext: LOOP LIMIT HIT! Possible infinite loop in script " +
                    $"'{_currentScript?.FilePath ?? "unknown"}' at step {_stepIndex}. Stopping.");
                EndScript();
                return;
            }

            var step = _currentScript.Steps[_stepIndex];
            LogEngine($"  Step[{_stepIndex}]: {step.Type}, cmd={step.CommandName}, spk={step.Speaker}, txt={(step.Text ?? "")[..Math.Min(20, step.Text?.Length ?? 0)]}");

            switch (step.Type)
            {
                case ScriptStepType.Label:
                    LogEngine($"    -> Label '{step.Label}', skipping");
                    _stepIndex++;
                    break;

                case ScriptStepType.Text:
                    LogEngine($"    -> Text, invoking OnText, returning");
                    OnText?.Invoke(step.Speaker, step.Text ?? "");
                    return;

                case ScriptStepType.Command:
                    _paused = false;
                    LogEngine($"    -> Command, executing...");
                    ExecuteCommand(step);
                    LogEngine($"    -> Command done, _paused={_paused}");
                    if (_paused) { LogEngine("    -> Paused, returning"); return; }
                    _stepIndex++;
                    break;

                case ScriptStepType.Jump:
                    if (step.Parameters.TryGetValue("target", out var label) &&
                        label is string targetLabel && !string.IsNullOrEmpty(targetLabel))
                    {
                        LogEngine($"    -> Jump to '{targetLabel}'");
                        var targetIdx = FindLabel(targetLabel);
                        if (targetIdx >= 0)
                        {
                            LogEngine($"    -> Found at index {targetIdx}");
                            _stepIndex = targetIdx;
                            break;
                        }
                        LogEngine($"    -> Label not found!");
                    }
                    _stepIndex++;
                    break;

                case ScriptStepType.Choice:
                    LogEngine($"    -> Choice, invoking OnChoice, returning");
                    var choiceTexts = step.Choices ?? new List<string>();
                    var choiceTargets = step.Condition?.Split(',').ToList() ?? new List<string>();
                    OnChoice?.Invoke(choiceTexts, choiceTargets);
                    return;

                case ScriptStepType.If:
                    LogEngine($"    -> If, condition='{step.Condition}'");
                    var conditionResult = EvaluateCondition(step.Condition ?? "");
                    if (conditionResult)
                    {
                        ExecuteInline(step.IfBody ?? new List<ScriptStep>());
                    }
                    else if (step.ElseIfs != null)
                    {
                        bool handled = false;
                        foreach (var (elifCond, elifBody) in step.ElseIfs)
                        {
                            if (EvaluateCondition(elifCond))
                            {
                                ExecuteInline(elifBody);
                                handled = true;
                                break;
                            }
                        }
                        if (!handled && step.ElseBody != null)
                            ExecuteInline(step.ElseBody);
                    }
                    else if (step.ElseBody != null)
                    {
                        ExecuteInline(step.ElseBody);
                    }
                    _stepIndex++;
                    break;

                default:
                    // 未知步骤类型 —— 跳过
                    _stepIndex++;
                    break;
            }
        }

        // 已到达步骤末尾
        EndScript();
    }

    /// <summary>执行一组内联步骤（if/choice 语句体）</summary>
    private void ExecuteInline(List<ScriptStep> body)
    {
        foreach (var step in body)
        {
            switch (step.Type)
            {
                case ScriptStepType.Text:
                    OnText?.Invoke(step.Speaker, step.Text ?? "");
                    break;
                case ScriptStepType.Command:
                    _paused = false;
                    ExecuteCommand(step);
                    if (_paused) return;
                    break;
                case ScriptStepType.Jump:
                    if (step.Parameters.TryGetValue("target", out var label) && label is string l)
                    {
                        var targetIdx = FindLabel(l);
                        if (targetIdx >= 0) _stepIndex = targetIdx;
                    }
                    break;
                case ScriptStepType.Choice:
                    OnChoice?.Invoke(step.Choices ?? new(), step.Condition?.Split(',').ToList() ?? new());
                    break;
            }
        }
    }

    private void ExecuteCommand(ScriptStep step)
    {
        var cmd = _registry.Resolve(step.CommandName ?? "");
        if (cmd == null) return;

        var context = new ScriptCommandContext
        {
            Arguments = step.Parameters
                .Where(p => p.Key.StartsWith("_arg"))
                .OrderBy(p => int.Parse(p.Key[4..]))
                .Select(p => p.Value)
                .ToList(),
            NamedArgs = step.Parameters
                .Where(p => !p.Key.StartsWith("_arg"))
                .ToDictionary(p => p.Key, p => p.Value),
            Engine = this,
            FlowBindings = null
        };

        cmd.Execute(context);
        OnCommand?.Invoke(cmd.Name, step.Parameters);
    }

    private int FindLabel(string name)
    {
        if (_currentScript == null) return -1;
        for (int i = 0; i < _currentScript.Steps.Count; i++)
        {
            if (_currentScript.Steps[i].Type == ScriptStepType.Label &&
                string.Equals(_currentScript.Steps[i].Label, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>求值条件表达式，支持 flag.xxx、!flag.xxx、true/false</summary>
    private bool EvaluateCondition(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return false;
        var expr = condition.Trim();

        // 取反
        bool negate = false;
        if (expr.StartsWith('!'))
        {
            negate = true;
            expr = expr[1..].Trim();
        }

        bool result = expr switch
        {
            "true" => true,
            "false" => false,
            _ => expr.StartsWith("flag.") ? GetFlag(expr[5..]) : true
        };

        return negate ? !result : result;
    }

    private void EndScript()
    {
        _isRunning = false;
        OnScriptEnd?.Invoke();
    }

    // ---- 存档状态 ----

    /// <summary>导出当前脚本执行状态用于存档</summary>
    public ScriptSaveState SaveState()
    {
        return new ScriptSaveState
        {
            StepIndex = _stepIndex,
            Variables = new Dictionary<string, object?>(_variables),
            Flags = new Dictionary<string, bool>(_flags)
        };
    }

    /// <summary>从存档恢复脚本执行状态</summary>
    public void LoadState(ScriptSaveState state)
    {
        _variables.Clear();
        foreach (var kv in state.Variables)
            _variables[kv.Key] = kv.Value;

        _flags.Clear();
        foreach (var kv in state.Flags)
            _flags[kv.Key] = kv.Value;

        _stepIndex = state.StepIndex;
        _isRunning = true;
    }

    // ---- 变量/标记访问 ----

    public void SetVariable(string name, object? value) => _variables[name] = value;
    public object? GetVariable(string name) => _variables.TryGetValue(name, out var v) ? v : null;
    public void SetFlag(string flag, bool value) => _flags[flag] = value;
    public bool GetFlag(string flag) => _flags.TryGetValue(flag, out var v) && v;

    /// <summary>获取所有标记（用于调试）</summary>
    public IReadOnlyDictionary<string, bool> GetAllFlags() => _flags;
    /// <summary>当前脚本名</summary>
    public string? CurrentScriptName => _currentScript?.FilePath;
    /// <summary>当前步骤索引（用于存档）</summary>
    public int CurrentStepIndex => _stepIndex;
}

/// <summary>脚本存档状态，可序列化</summary>
public class ScriptSaveState
{
    public int StepIndex { get; set; }
    public Dictionary<string, object?> Variables { get; set; } = new();
    public Dictionary<string, bool> Flags { get; set; } = new();
}
