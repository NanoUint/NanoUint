using System.IO;
using System.Reflection;
using NanoUint.Scripting;

namespace NanoUint;

/// <summary>
/// 脚本管理器。类似 Unity 的场景脚本系统，是 .vns 管道的高层 API。
/// 游戏端通过 ScriptManager.Load/Run 加载和执行视觉小说脚本。
/// </summary>
public static class ScriptManager
{
    private static ScriptEngine? _engine;
    private static CompiledScript? _currentScript;

    // ── 事件 ──

    /// <summary>对话文本事件：(speaker, text)</summary>
    public static event Action<string?, string>? OnDialogue;

    /// <summary>选择事件：(choiceTexts, choiceTargets)</summary>
    public static event Action<List<string>, List<string>>? OnChoice;

    /// <summary>脚本执行结束</summary>
    public static event Action? OnScriptEnd;

    /// <summary>任意命令执行时触发</summary>
    public static event Action<string, Dictionary<string, object?>>? OnCommand;

    public static bool IsRunning => _engine?.IsRunning ?? false;
    public static string? CurrentScriptName => _engine?.CurrentScriptName;

    // ── 加载 ──

    /// <summary>从嵌入资源路径加载 .vns 脚本。</summary>
    public static CompiledScript Load(string resourcePath)
    {
        // 1. 尝试从 AssetDatabase 加载（嵌入资源）
        if (AssetDatabase.Exists(resourcePath))
        {
            var scriptAsset = AssetDatabase.Load<ScriptAsset>(resourcePath);
            Debug.Log($"ScriptManager: Loaded '{resourcePath}' from embedded resources ({scriptAsset.Source.Length} chars)");
            return CompileFromSource(scriptAsset.Source, resourcePath);
        }

        // 2. 尝试从文件系统加载
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

    // ── 执行 ──

    /// <summary>运行已编译的脚本。执行前预加载所有引用的资源。</summary>
    public static void Run(CompiledScript script, string? startLabel = null)
    {
        var engine = GetOrCreateEngine();

        // 先注册基本命令
        EnsureCommandsRegistered(engine);

        _currentScript = script;

        // 预加载脚本中引用的所有资源（避免执行时阻塞 UI 线程）
        PreloadScriptAssets(script);

        // 绑定引擎事件到静态事件
        engine.OnText += (speaker, text) => OnDialogue?.Invoke(speaker, text);
        engine.OnChoice += (texts, targets) => OnChoice?.Invoke(texts, targets);
        engine.OnScriptEnd += () => OnScriptEnd?.Invoke();
        engine.OnCommand += (name, args) => OnCommand?.Invoke(name, args);

        engine.Run(script, startLabel);
        Debug.Log($"ScriptManager: Running '{script.FilePath}'{(startLabel != null ? $" @ {startLabel}" : "")}");
    }

    /// <summary>
    /// 预加载编译脚本中所有引用的资源（背景、立绘、音频）。
    /// 这确保脚本执行时资源已缓存，不会阻塞 UI 线程。
    /// </summary>
    private static void PreloadScriptAssets(CompiledScript script)
    {
        // 需要预加载资源的命令及其参数
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
                    // 预热 BitmapImage 缓存，避免首次渲染时解码 PNG 阻塞
                    Rendering.WpfRenderer.WarmupBitmap(sprite);
                    loaded++;
                }
            }
            else if (audioCommands.Contains(cmd))
            {
                // 预加载音频数据到内存缓存
                var clip = AssetDatabase.Load<AudioClip>(path);
                if (clip != null) loaded++;
                // 同时预热 temp 文件提取（路径缓存 + 文件缓存）
                var s = AssetDatabase.OpenStream(path);
                s?.Dispose();
            }
        }

        if (loaded > 0)
            Debug.Log($"ScriptManager: Preloaded {loaded} assets for '{script.FilePath}'");
    }

    /// <summary>直接运行已加载的脚本。</summary>
    public static void Run(CompiledScript script) => Run(script, null);

    /// <summary>继续执行（玩家点击推进后调用）。</summary>
    public static void Continue()
    {
        _engine?.Continue();
    }

    /// <summary>选择选项（ChoiceGroup 选择后调用）。</summary>
    public static void SelectChoice(int index)
    {
        _engine?.SelectChoice(index);
    }

    /// <summary>停止脚本执行。</summary>
    public static void Stop()
    {
        _engine?.Stop();
    }

    /// <summary>跳转到标签。</summary>
    public static void JumpToLabel(string label)
    {
        _engine?.JumpToLabel(label);
    }

    // ── 变量/标志 ──

    public static void SetFlag(string name, bool value) => _engine?.SetFlag(name, value);
    public static bool GetFlag(string name) => _engine?.GetFlag(name) ?? false;
    public static void SetVariable(string name, object? value) => _engine?.SetVariable(name, value);
    public static object? GetVariable(string name) => _engine?.GetVariable(name);

    // ── 内部 ──

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

    private static void EnsureCommandsRegistered(ScriptEngine engine)
    {
        if (_commandsRegistered) return;
        _commandsRegistered = true;

        // 注册引擎内置命令（EngineCommands 使用静态 AudioManager + Scene API）
        engine.ScanCommands(new EngineCommands());
        Debug.Log("ScriptManager: Engine commands registered.");
    }

    private static void RegisterInternalCommands(ScriptEngine engine)
    {
        // 扫描当前程序集中所有 [RegistryInScript] 方法
        var asm = Assembly.GetExecutingAssembly();
        engine.ScanCommands(asm);
    }
}
