using System.IO;
using NanoUint.Models;

namespace NanoUint.Scripting;

/// <summary>
/// Top-level script engine that ties together lexing, parsing, compilation,
/// and execution of .vns visual novel scripts.
///
/// This is the main API that game projects use to load and run scripts.
/// </summary>
public class ScriptEngine
{
    private readonly ScriptCommandRegistry _registry;
    private readonly Dictionary<string, CompiledScript> _loadedScripts = new();
    private CompiledScript? _currentScript;
    private int _stepIndex;
    private bool _isRunning;
    private bool _paused;

    // Execution state
    private readonly Dictionary<string, object?> _variables = new();
    private readonly Dictionary<string, bool> _flags = new();
    private readonly Stack<(CompiledScript Script, int StepIndex)> _callStack = new();
    /// <summary>Fired when a text line should be displayed (narration or dialogue)</summary>
    public event Action<string?, string>? OnText;

    /// <summary>Fired when a choice should be presented to the player</summary>
    public event Action<List<string>, List<string>>? OnChoice;

    /// <summary>Fired when any command is executed</summary>
    public event Action<string, Dictionary<string, object?>>? OnCommand;

    /// <summary>Fired when the script ends or jumps to a scene</summary>
    public event Action? OnScriptEnd;

    /// <summary>Current script step, or null if not running</summary>
    public ScriptStep? CurrentStep => _isRunning && _stepIndex < (_currentScript?.Steps.Count ?? 0)
        ? _currentScript!.Steps[_stepIndex] : null;

    public ScriptCommandRegistry Registry => _registry;
    public bool IsRunning => _isRunning;

    public ScriptEngine()
    {
        _registry = new ScriptCommandRegistry();
    }

    /// <summary>Register a command implementation from the game project</summary>
    public void RegisterCommand(IScriptCommand command) => _registry.Register(command);

    /// <summary>Load a .vns script from file path</summary>
    public CompiledScript LoadFromFile(string filePath)
    {
        var source = File.ReadAllText(filePath);
        return LoadFromSource(source, filePath);
    }

    /// <summary>Load a .vns script from a string</summary>
    public CompiledScript LoadFromSource(string source, string filePath = "")
    {
        var lexer = new VnsLexer(source, filePath);
        var tokens = lexer.Tokenize();

        var parser = new VnsParser(tokens, filePath);
        var document = parser.Parse();

        var compiler = new VnsCompiler(_registry);
        var compiled = compiler.Compile(document);

        _loadedScripts[filePath] = compiled;
        return compiled;
    }

    /// <summary>Start executing a compiled script</summary>
    public void Run(CompiledScript script, string? startLabel = null)
    {
        _currentScript = script;
        _stepIndex = 0;
        _isRunning = true;
        _callStack.Clear();

        // If a start label is specified, jump to it
        if (startLabel != null)
        {
            JumpToLabel(startLabel);
            return;
        }

        // If there's a #start label, jump there
        var startIdx = FindLabel("start");
        if (startIdx >= 0)
        {
            _stepIndex = startIdx;
        }

        ExecuteNext();
    }

    /// <summary>Request the engine to pause after the current command completes</summary>
    public void RequestPause() => _paused = true;

    /// <summary>Continue execution (called after player advances past text/choice)</summary>
    public void Continue()
    {
        if (!_isRunning || _currentScript == null) return;
        _paused = false;
        _stepIndex++;
        if (_stepIndex >= _currentScript.Steps.Count)
        {
            EndScript();
            return;
        }
        ExecuteNext();
    }

    /// <summary>Select a choice by index (0-based)</summary>
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

    /// <summary>Jump to a named label in the current script</summary>
    public void JumpToLabel(string labelName)
    {
        var idx = FindLabel(labelName);
        if (idx >= 0)
        {
            _stepIndex = idx;
            ExecuteNext();
        }
    }

    /// <summary>Stop execution</summary>
    public void Stop()
    {
        _isRunning = false;
        _currentScript = null;
    }

    // ---- Internal execution ----

    private void ExecuteNext()
    {
        if (!_isRunning || _currentScript == null) return;

        while (_stepIndex < _currentScript.Steps.Count)
        {
            var step = _currentScript.Steps[_stepIndex];

            switch (step.Type)
            {
                case ScriptStepType.Label:
                    _stepIndex++;
                    continue;

                case ScriptStepType.Text:
                    OnText?.Invoke(step.Speaker, step.Text ?? "");
                    return; // Wait for player to advance

                case ScriptStepType.Command:
                    _paused = false;
                    ExecuteCommand(step);
                    if (_paused) return; // Command requested pause — wait for Continue()
                    _stepIndex++;
                    continue;

                case ScriptStepType.Jump:
                    if (step.Parameters.TryGetValue("target", out var label) && label is string l)
                    {
                        JumpToLabel(l);
                        return;
                    }
                    _stepIndex++;
                    continue;

                case ScriptStepType.Choice:
                    var choiceTexts = step.Choices ?? new List<string>();
                    var choiceTargets = step.Condition?.Split(',').ToList() ?? new List<string>();
                    OnChoice?.Invoke(choiceTexts, choiceTargets);
                    return; // Wait for player choice

                case ScriptStepType.If:
                    var result = EvaluateCondition(step.Condition ?? "");
                    if (result)
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
                    continue;

                default:
                    _stepIndex++;
                    continue;
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
                    ExecuteCommand(step);
                    break;
                case ScriptStepType.Jump:
                    if (step.Parameters.TryGetValue("target", out var label) && label is string l)
                        JumpToLabel(l);
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
        return condition.Trim() switch
        {
            "true" => true,
            "false" => false,
            _ => true // simplified — real impl would parse expressions
        };
    }

    private void EndScript()
    {
        _isRunning = false;
        OnScriptEnd?.Invoke();
    }

    // ---- Variable/flag access ----

    public void SetVariable(string name, object? value) => _variables[name] = value;
    public object? GetVariable(string name) => _variables.TryGetValue(name, out var v) ? v : null;
    public void SetFlag(string flag, bool value) => _flags[flag] = value;
    public bool GetFlag(string flag) => _flags.TryGetValue(flag, out var v) && v;
}
