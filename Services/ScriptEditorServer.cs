using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace NanoUint.Services;

/// <summary>
/// 内嵌脚本编辑器 Web 服务器，监听内网 4400 端口。
/// 提供 .vns 脚本的在线编辑、保存和热重载功能。
/// </summary>
public class ScriptEditorServer
{
    private HttpListener _listener;
    private readonly string _scriptsDir;
    private readonly Action<string>? _onHotReload;
    private CancellationTokenSource? _cts;
    private string _activeUrl = "http://localhost:4400/";

    public bool IsRunning { get; private set; }
    public string EditorUrl => _activeUrl.TrimEnd('/');

    /// <param name="scriptsDir">脚本所在目录</param>
    /// <param name="onHotReload">热重载回调，传入文件名</param>
    public ScriptEditorServer(string scriptsDir, Action<string>? onHotReload = null)
    {
        _scriptsDir = scriptsDir;
        _onHotReload = onHotReload;
        _listener = new HttpListener();
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();

        // 尝试 LAN 访问（需要管理员），失败则降级为 localhost
        try
        {
            _listener.Prefixes.Add("http://+:4400/");
            _listener.Start();
            _activeUrl = "http://+:4400/";
            DebugConsole.Log("Editor", "脚本编辑器已启动 (LAN): http://localhost:4400");
        }
        catch (HttpListenerException)
        {
            // Start() 失败后 listener 已被释放，需重建
            _listener.Close();
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:4400/");
            _listener.Start();
            _activeUrl = "http://localhost:4400/";
            DebugConsole.Log("Editor", "脚本编辑器已启动 (仅本地): http://localhost:4400");
            DebugConsole.Log("Editor", "提示: 以管理员运行可开启局域网访问");
        }

        IsRunning = true;
        Task.Run(() => ListenLoop(_cts.Token));
    }

    public void Stop()
    {
        IsRunning = false;
        _cts?.Cancel();
        _listener.Stop();
        _listener.Close();
        DebugConsole.Log("Editor", "脚本编辑器已停止");
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx));
            }
            catch when (ct.IsCancellationRequested) { break; }
            catch (HttpListenerException) { break; }
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url!.AbsolutePath;
            var method = ctx.Request.HttpMethod;

            if (path == "/" || path == "/index.html")
                ServeEditorHtml(ctx);
            else if (path == "/api/scripts" && method == "GET")
                ListScripts(ctx);
            else if (path.StartsWith("/api/scripts/") && method == "GET")
                ReadScript(ctx, path["/api/scripts/".Length..]);
            else if (path.StartsWith("/api/scripts/") && method == "PUT")
                SaveScript(ctx, path["/api/scripts/".Length..]);
            else if (path == "/api/reload" && method == "POST")
                HotReload(ctx);
            else
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
            }
        }
        catch (Exception ex)
        {
            DebugConsole.LogError("Editor", ex);
            ctx.Response.StatusCode = 500;
            ctx.Response.Close();
        }
    }

    private void ListScripts(HttpListenerContext ctx)
    {
        try
        {
            if (!Directory.Exists(_scriptsDir))
            {
                RespondJson(ctx, new { error = "脚本目录不存在", dir = _scriptsDir });
                return;
            }
            var files = Directory.GetFiles(_scriptsDir, "*.vns")
                .Select(f => new { name = Path.GetFileName(f), size = new FileInfo(f).Length, modified = File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm:ss") })
                .ToList();
            RespondJson(ctx, files);
        }
        catch (Exception ex)
        {
            RespondJson(ctx, new { error = ex.Message });
        }
    }

    private void ReadScript(HttpListenerContext ctx, string name)
    {
        var path = Path.Combine(_scriptsDir, Path.GetFileName(name));
        if (!File.Exists(path))
        {
            ctx.Response.StatusCode = 404;
            RespondJson(ctx, new { error = "文件不存在" });
            return;
        }
        var content = File.ReadAllText(path, Encoding.UTF8);

        // 读取积木JSON（如果存在）
        string? blocklyJson = null;
        var blocklyPath = Path.ChangeExtension(path, ".blocks.json");
        if (File.Exists(blocklyPath))
            blocklyJson = File.ReadAllText(blocklyPath, Encoding.UTF8);

        RespondJson(ctx, new { name = Path.GetFileName(path), content, blocklyJson });
    }

    private void SaveScript(HttpListenerContext ctx, string name)
    {
        using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
        var body = reader.ReadToEnd();
        var json = JsonDocument.Parse(body);
        var content = json.RootElement.GetProperty("content").GetString() ?? "";

        var path = Path.Combine(_scriptsDir, Path.GetFileName(name));
        if (File.Exists(path))
        {
            var backup = path + ".bak";
            File.Copy(path, backup, true);
        }
        File.WriteAllText(path, content, Encoding.UTF8);

        // 保存积木JSON（与.vns同名的.json文件）
        if (json.RootElement.TryGetProperty("blocklyJson", out var blocklyEl))
        {
            var blocklyPath = Path.ChangeExtension(path, ".blocks.json");
            File.WriteAllText(blocklyPath, blocklyEl.GetString() ?? "", Encoding.UTF8);
        }

        DebugConsole.Log("Editor", $"已保存: {Path.GetFileName(path)}");
        RespondJson(ctx, new { ok = true, name = Path.GetFileName(path) });
    }

    private void HotReload(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
        var body = reader.ReadToEnd();
        string? filename = null;
        if (!string.IsNullOrEmpty(body))
        {
            try
            {
                var json = JsonDocument.Parse(body);
                filename = json.RootElement.GetProperty("file").GetString();
            }
            catch { }
        }

        DebugConsole.Log("Editor", $"热重载请求: {filename ?? "(当前)"}");
        _onHotReload?.Invoke(filename ?? "");
        RespondJson(ctx, new { ok = true });
    }

    private static void RespondJson(HttpListenerContext ctx, object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    private void ServeEditorHtml(HttpListenerContext ctx)
    {
        var html = GetEditorHtml();
        var bytes = Encoding.UTF8.GetBytes(html);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    private static string? _cachedHtml;
    private string GetEditorHtml()
    {
        if (_cachedHtml != null) return _cachedHtml;
        using var stream = typeof(ScriptEditorServer).Assembly
            .GetManifestResourceStream("NanoUint.Resources.editor.html");
        if (stream == null) return "<h1>编辑器加载失败</h1>";
        using var reader = new StreamReader(stream, Encoding.UTF8);
        _cachedHtml = reader.ReadToEnd();
        return _cachedHtml;
    }
}
