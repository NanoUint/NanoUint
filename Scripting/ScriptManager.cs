using System.IO;
using System.Reflection;
using NanoUint.Scripting;

namespace NanoUint;

/// <summary>High-level API for loading and running .vns scripts.</summary>
public static class ScriptManager
{
    private static ScriptEngine? _engine;
    private static CompiledScript? _currentScript;

    #region Events

    /// <summary>Dialogue text event: (speaker, text)</summary>
    public static event Action<string?, string>? OnDialogue;

    /// <summary>Choice event: (choiceTexts, choiceTargets)</summary>
    public static event Action<List<string>, List<string>>? OnChoice;

    /// <summary>Script execution ended</summary>
    public static event Action? OnScriptEnd;

    /// <summary>Raised when any command executes</summary>
    public static event Action<string, Dictionary<string, object?>>? OnCommand;

    public static bool IsRunning => _engine?.IsRunning ?? false;
    public static string? CurrentScriptName => _engine?.CurrentScriptName;

    #endregion

    #region Loading

    /// <summary>Loads a .vns script from an embedded resource path.</summary>
    public static CompiledScript Load(string resourcePath)
    {
        if (AssetDatabase.Exists(resourcePath))
        {
            var scriptAsset = AssetDatabase.Load<ScriptAsset>(resourcePath);
            if (scriptAsset != null)
            {
                Debug.Log($"ScriptManager: Loaded '{resourcePath}' from embedded resources ({scriptAsset.Source.Length} chars)");
                return CompileFromSource(scriptAsset.Source, resourcePath);
            }
        }

        if (File.Exists(resourcePath))
        {
            var source = File.ReadAllText(resourcePath);
            Debug.Log($"ScriptManager: Loaded '{resourcePath}' from filesystem ({source.Length} chars)");
            return CompileFromSource(source, resourcePath);
        }

        throw new FileNotFoundException($"Script not found: '{resourcePath}'");
    }

    private static CompiledScript CompileFromSource(string source, string path)
    {
        var engine = GetOrCreateEngine();
        var lexer = new VnsLexer(source, path);
        var tokens = lexer.Tokenize();

        var parser = new VnsParser(tokens, path);
        var document = parser.Parse();

        if (parser.Errors.Count > 0)
        {
            foreach (var err in parser.Errors)
                Debug.LogError($"Script parse error: {err}");
            throw new InvalidOperationException($"Script parsing failed with {parser.Errors.Count} errors.");
        }

        var compiler = new VnsCompiler(engine.Registry);
        var compiled = compiler.Compile(document);
        compiled.FilePath = path;
        return compiled;
    }

    #endregion

    #region Execution

    /// <summary>Runs a compiled script, preloading its referenced assets.</summary>
    public static void Run(CompiledScript script, string? startLabel = null)
    {
        var engine = GetOrCreateEngine();

        EnsureCommandsRegistered(engine);

        _currentScript = script;

        PreloadScriptAssets(script);

        // Unsubscribe previous handlers to prevent N-fold event accumulation on repeated Run() calls
        engine.OnText -= OnEngineText;
        engine.OnChoice -= OnEngineChoice;
        engine.OnScriptEnd -= OnEngineScriptEnd;
        engine.OnCommand -= OnEngineCommand;

        engine.OnText += OnEngineText;
        engine.OnChoice += OnEngineChoice;
        engine.OnScriptEnd += OnEngineScriptEnd;
        engine.OnCommand += OnEngineCommand;

        engine.Run(script, startLabel);
        Debug.Log($"ScriptManager: Running '{script.FilePath}'{(startLabel != null ? $" @ {startLabel}" : "")}");
    }

    private static void OnEngineText(string? speaker, string text) => OnDialogue?.Invoke(speaker, text);
    private static void OnEngineChoice(List<string> texts, List<string> targets) => OnChoice?.Invoke(texts, targets);
    private static void OnEngineScriptEnd() => OnScriptEnd?.Invoke();
    private static void OnEngineCommand(string name, Dictionary<string, object?> args) => OnCommand?.Invoke(name, args);

    private static void PreloadScriptAssets(CompiledScript script)
    {
        var imageCommands = new HashSet<string> { "bg", "sprite" };
        var audioCommands = new HashSet<string> { "bgm", "sfx", "voice" };

        int loaded = 0;
        foreach (var step in script.Steps)
        {
            if (step.Type != ScriptStepType.Command) continue;
            var cmd = step.CommandName ?? "";

            var path = step.Parameters.TryGetValue("_arg0", out var p) ? p as string : null;
            if (string.IsNullOrEmpty(path)) continue;

            if (imageCommands.Contains(cmd))
            {
                var sprite = AssetDatabase.Load<Sprite>(path);
                if (sprite != null)
                {
                    // Warm up the BitmapImage cache (directly via ResourceManager, no WPF renderer dependency)
                    ResourceManager.WarmupSync(sprite.Path);
                    loaded++;
                }
            }
            else if (audioCommands.Contains(cmd))
            {
                // Preload audio data into the in-memory cache
                var clip = AssetDatabase.Load<AudioClip>(path);
                if (clip != null) loaded++;
                // Also warm up temp-file extraction (path cache + file cache)
                var s = AssetDatabase.OpenStream(path);
                s?.Dispose();
            }
        }

        if (loaded > 0)
            Debug.Log($"ScriptManager: Preloaded {loaded} assets for '{script.FilePath}'");
    }

    /// <summary>Runs an already-loaded script directly.</summary>
    public static void Run(CompiledScript script) => Run(script, null);

    /// <summary>Resumes execution after the player advances.</summary>
    public static void Continue()
    {
        _engine?.Continue();
    }

    /// <summary>Selects a choice by index.</summary>
    public static void SelectChoice(int index)
    {
        _engine?.SelectChoice(index);
    }

    /// <summary>Stops script execution.</summary>
    public static void Stop()
    {
        _engine?.Stop();
    }

    /// <summary>Jumps to a label.</summary>
    public static void JumpToLabel(string label)
    {
        _engine?.JumpToLabel(label);
    }

    #endregion

    #region Variables / flags

    public static void SetFlag(string name, bool value) => _engine?.SetFlag(name, value);
    public static bool GetFlag(string name) => _engine?.GetFlag(name) ?? false;
    public static void SetVariable(string name, object? value) => _engine?.SetVariable(name, value);
    public static object? GetVariable(string name) => _engine?.GetVariable(name);

    /// <summary>Exports the current script execution state for saving.</summary>
    public static ScriptSaveState? SaveState() => _engine?.SaveState();

    /// <summary>Restores script execution state from a save.</summary>
    public static void LoadState(ScriptSaveState state) => _engine?.LoadState(state);

    /// <summary>Gets all flags of the script engine (for saving).</summary>
    public static IReadOnlyDictionary<string, bool> GetAllFlags()
        => _engine?.GetAllFlags() ?? new Dictionary<string, bool>();

    #endregion

    #region Internal

    private static ScriptEngine GetOrCreateEngine()
    {
        if (_engine == null)
        {
            _engine = new ScriptEngine();
            RegisterInternalCommands(_engine);
        }
        return _engine;
    }

    private static bool _commandsRegistered;
    private static object[]? _gameCommandInstances;

    /// <summary>Registers game-layer VNS commands.</summary>
    public static void RegisterGameCommands(params object[] instances)
    {
        _gameCommandInstances = instances;
        if (_engine != null)
        {
            _engine.ScanCommands(instances);
            Debug.Log($"ScriptManager: {instances.Length} game command instance(s) registered.");
        }
    }

    private static void EnsureCommandsRegistered(ScriptEngine engine)
    {
        if (_commandsRegistered) return;
        _commandsRegistered = true;

        // Register engine built-in commands (EngineCommands uses the static AudioManager + Scene API)
        engine.ScanCommands(new EngineCommands());
        Debug.Log("ScriptManager: Engine commands registered.");

        if (_gameCommandInstances != null)
        {
            engine.ScanCommands(_gameCommandInstances);
            Debug.Log($"ScriptManager: Game commands registered ({_gameCommandInstances.Length} instances).");
        }
    }

    private static void RegisterInternalCommands(ScriptEngine engine)
    {
        var asm = Assembly.GetExecutingAssembly();
        engine.ScanCommands(asm);
    }
    #endregion
}
