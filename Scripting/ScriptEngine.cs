using System.IO;
using System.Reflection;
using NanoUint.Diagnostics;

namespace NanoUint.Scripting;

/// <summary>Top-level engine that loads, compiles, and runs .vns scripts.</summary>
public class ScriptEngine
{
    private readonly ScriptCommandRegistry _registry;
    private readonly Dictionary<string, CompiledScript> _loadedScripts = new();
    private CompiledScript? _currentScript;
    private int _stepIndex;
    private bool _isRunning;
    private bool _paused;

    private readonly Dictionary<string, object?> _variables = new();
    private readonly Dictionary<string, bool> _flags = new();
    private readonly Stack<(CompiledScript Script, int StepIndex)> _callStack = new();
    /// <summary>Raised when a text line should be shown (narration or dialogue)</summary>
    public event Action<string?, string>? OnText;

    /// <summary>Raised when choices should be presented to the player</summary>
    public event Action<List<string>, List<string>>? OnChoice;

    /// <summary>Raised when any command executes</summary>
    public event Action<string, Dictionary<string, object?>>? OnCommand;

    /// <summary>Raised when the script ends or jumps to a scene</summary>
    public event Action? OnScriptEnd;

    /// <summary>Triggers text display from external commands.</summary>
    public void RaiseText(string? speaker, string text) => OnText?.Invoke(speaker, text);

    /// <summary>Current script step, or null if not running</summary>
    public ScriptStep? CurrentStep => _isRunning && _stepIndex < (_currentScript?.Steps.Count ?? 0)
        ? _currentScript!.Steps[_stepIndex] : null;

    public ScriptCommandRegistry Registry => _registry;
    public bool IsRunning => _isRunning;

    public ScriptEngine()
    {
        _registry = new ScriptCommandRegistry();
    }

    /// <summary>Registers a command implementation from the game project</summary>
    public void RegisterCommand(IScriptCommand command) => _registry.Register(command);

    /// <summary>Scans an assembly for [RegistryInScript]-decorated static methods and auto-registers them.</summary>
    public void ScanCommands(Assembly assembly) =>
        ScriptCommandScanner.ScanAssembly(this, assembly);

    /// <summary>Scans object instances for [RegistryInScript]-decorated methods.</summary>
    public void ScanCommands(params object[] instances) =>
        ScriptCommandScanner.ScanInstances(this, instances);

    /// <summary>Loads a .vns script from a file path</summary>
    public CompiledScript LoadFromFile(string filePath)
    {
        LogEngine($"LoadFromFile: reading {filePath}");
        var source = File.ReadAllText(filePath);
        LogEngine($"LoadFromFile: read {source.Length} chars");
        return LoadFromSource(source, filePath);
    }

    /// <summary>Loads a .vns script from a string</summary>
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

    /// <summary>Starts executing the compiled script</summary>
    public void Run(CompiledScript script, string? startLabel = null)
    {
        _currentScript = script;
        _stepIndex = 0;
        _isRunning = true;
        _callStack.Clear();

        if (startLabel != null)
        {
            var idx = FindLabel(startLabel);
            if (idx >= 0) _stepIndex = idx;
        }
        else
        {
            var startIdx = FindLabel("start");
            if (startIdx >= 0) _stepIndex = startIdx;
        }

        LogEngine($"Run: _stepIndex={_stepIndex}, steps={_currentScript.Steps.Count}, dispatching ExecuteNext...");

        // Defer the first step via the Dispatcher
        LogEngine($"Run: dispatcher ok, posting...");
        Application.Default!.Dispatcher.BeginInvoke(() => {
                LogEngine($"Run: BeginInvoke fired, calling ExecuteNext");
                ExecuteNext();
                LogEngine($"Run: ExecuteNext returned");
            });
        LogEngine($"Run: BeginInvoke posted, Run returning");
    }

    /// <summary>Requests the engine to pause after the current command finishes</summary>
    public void RequestPause() => _paused = true;

    /// <summary>Resumes execution after the player skips text or picks a choice.</summary>
    public void Continue()
    {
        if (!_isRunning || _currentScript == null) return;

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

    /// <summary>Selects a choice by index (zero-based)</summary>
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

    /// <summary>Jumps to a named label in the current script</summary>
    public void JumpToLabel(string labelName)
    {
        var idx = FindLabel(labelName);
        if (idx >= 0)
        {
            _stepIndex = idx;
            ExecuteNext();
        }
    }

    /// <summary>Stops execution</summary>
    public void Stop()
    {
        _isRunning = false;
        _currentScript = null;
    }

    private static void LogEngine(string msg) => Logger.Trace("ScriptEngine", msg);

    private void ExecuteNext()
    {
        LogEngine($"ExecuteNext: _isRunning={_isRunning}, _stepIndex={_stepIndex}, steps={_currentScript?.Steps.Count}");
        if (!_isRunning || _currentScript == null) { LogEngine("ExecuteNext: abort - not running"); return; }

        // Skip labels and non-pausing commands until interactive content or all steps are done.
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
                    _stepIndex++;
                    break;
            }
        }

        EndScript();
    }

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

    private bool EvaluateCondition(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return false;
        var expr = condition.Trim();

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

    /// <summary>Exports current script execution state for saving</summary>
    public ScriptSaveState SaveState()
    {
        return new ScriptSaveState
        {
            StepIndex = _stepIndex,
            Variables = new Dictionary<string, object?>(_variables),
            Flags = new Dictionary<string, bool>(_flags)
        };
    }

    /// <summary>Restores script execution state from a save</summary>
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

    public void SetVariable(string name, object? value) => _variables[name] = value;
    public object? GetVariable(string name) => _variables.TryGetValue(name, out var v) ? v : null;
    public void SetFlag(string flag, bool value) => _flags[flag] = value;
    public bool GetFlag(string flag) => _flags.TryGetValue(flag, out var v) && v;

    /// <summary>Gets all flags (for debugging)</summary>
    public IReadOnlyDictionary<string, bool> GetAllFlags() => _flags;
    /// <summary>Current script name</summary>
    public string? CurrentScriptName => _currentScript?.FilePath;
    /// <summary>Current step index (for saving)</summary>
    public int CurrentStepIndex => _stepIndex;
}

/// <summary>Script save state, serializable</summary>
public class ScriptSaveState
{
    public int StepIndex { get; set; }
    public Dictionary<string, object?> Variables { get; set; } = new();
    public Dictionary<string, bool> Flags { get; set; } = new();
}
