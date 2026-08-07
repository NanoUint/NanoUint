# NanoUint

基于 WPF 的微型视觉小说引擎（"A Nano Unity Game Engine for Visual Novels base on WPF"）。
为《Steins;Gate X》同人项目自研：Unity 风格的对象/组件/协程模型 + WPF 渲染 + 脚本命令系统。

## 架构概览

```
┌─────────────────────────────────────────────────────────┐
│ 游戏项目（如 SteinsGateX）                                │
│   ScreenBase 子类 · Chapter 协程 · 业务逻辑                │
└───────────────┬─────────────────────────────────────────┘
┌───────────────▼─────────────────────────────────────────┐
│ NanoUint（本库）                                          │
│  Core        Scene / GameObject / Component / Transform  │
│              InputManager / CanvasScaler / SceneManager  │
│  Components  SpriteRenderer / TextRenderer / 按钮 / 对话  │
│              选项 / 背景 / 音频 / 特效 / 时间轴(Tweener)    │
│  Rendering   WpfRenderer(增量渲染) / WpfEngineHost(主循环) │
│              ScenePreviewHost(编辑器预览)                  │
│  Coroutine   协程调度 + Yields 集合                        │
│  Scripting   VNS 脚本解析/编译/执行 + ScriptCommandRegistry │
│  AssetDatabase  资源加载(文件系统) + 缓存                   │
│  Audio       NAudio 音频管理（BGM/SFX/语音）               │
│  Animation   Ease 缓动函数                                │
│  Localization  多语言                                      │
│  Save        存档                                          │
│  Debugging   DebugConsole / DevPanel / InspectorTab / UE  │
└─────────────────────────────────────────────────────────┘
```

## 核心概念

### 场景与对象（Unity 风格）

```csharp
var scene = new Scene("Main");
var go = scene.AddObject("Logo");          // 自动触发 Awake
go.Transform.X = 0.5f;                     // 归一化坐标：0=左/顶，1=右/底
go.Transform.Y = 0.5f;
go.Transform.SortingOrder = 10;            // Z 深度，越大越靠前
go.Transform.Opacity = 1f;
go.Transform.SetParent(parent.Transform);  // 父子层级（世界坐标 = 累加父级）
```

- `GameObject`：容器，本身无行为，通过 `AddComponent<T>()` 组合
- `Component`：行为单元（Awake/Start/Update/OnDestroy）；`Behaviour` 支持协程
- `Transform`：每个对象必备，X/Y 为 0~1 归一化坐标（相对父对象），SortingOrder 决定渲染层级

### 组件清单（Components/）

| 组件 | 用途 |
|---|---|
| `SpriteRenderer` | 图片显示（Sprite/Tint/口型动画 MouthClosed/Half/Open） |
| `TextRenderer` | 纯文本（Content/FontSize/TextColor） |
| `SpriteButton` / `PassiveButton` | 图片按钮 / 热区按钮（OnClick/OnEnter/OnExit），SpriteButton 自动挂 PassiveButton |
| `BackgroundRenderer` | 全屏背景 + 双纹理交叉淡入（CrossfadeTo） |
| `DialogueBox` | 对话（SpeakerName/Show/打字机 ProgressiveReveal/TextSpeed） |
| `ChoiceGroup` | 选项组（Show/Inline/WaitForChoiceAsync） |
| `AudioSource` | 音频（Clip/IsLooping/Volume/Play/Stop/Pause） |
| `FlashOverlay` | 全屏闪色 |
| `AdvanceIndicator` | 继续阅读指示器 |
| `LineRenderer` | 线段（归一化坐标 SetPositions） |
| `HintRenderer` | 提示文本 |
| `Tweener` | 时间轴补间（配合 Animation/Ease） |
| `BacklogView` / `PhoneScreen` / `Slider` / `VideoPlayer` | 日志/电话界面/滑条/视频 |

### 协程

```csharp
StartCoroutine(MyRoutine());
IEnumerator MyRoutine()
{
    yield return new WaitForSeconds(0.5f);
    yield return new WaitForClick();
    // WaitForChoice / WaitUntil / WaitWhile / TypewriterDelay ...
}
```

### 脚本命令（Scripting/）

```csharp
ScriptEngine engine = ...;
engine.Registry.Register(new MyCommand());   // IScriptCommand
engine.Run("chapter1.vns");                  // VNS 脚本解析 → 编译 → 执行
```

### 资源（AssetDatabase/）

```csharp
AssetDatabase.AddSearchDirectory(@"C:\...\Resources");
var sprite = AssetDatabase.Load<Sprite>("Characters/okabe.png", 100f); // ppu：像素/世界单位
var audio  = AssetDatabase.Load<AudioClip>("BGM/theme.ogg");
```

### 渲染与宿主（Rendering/）

- `WpfRenderer`：增量渲染（只同步脏组件），CanvasScaler 支持分辨率自适应
- `WpfEngineHost`：游戏完整宿主（Window + 主循环 + 输入 + UE 调试面板），internal
- `ScenePreviewHost`（public）：**编辑器/外部嵌入用**——把引擎渲染进任意 Canvas，
  手动驱动帧循环，带 1920×1080 逻辑画布缩放：

```csharp
var host = new ScenePreviewHost(canvas, 1920, 1080);
// 往 host.Scene 添加 GameObject/组件（或由 NanoUintEditor.SceneRuntime 自动构建）
host.StartScene();   // 触发 Start
host.Start();        // 启动帧循环
// ...
host.Stop();         // 关闭时停止
```

## 游戏接入（IGameBootstrapper）

实现 `IGameBootstrapper` 并在入口调用宿主：

```csharp
public class MyGame : IGameBootstrapper
{
    public void OnStart()
    {
        AssetDatabase.AutoDetect();
        SceneManager.LoadScene(new Scene("Main"));
    }
}
// WpfEngineHost.Run 在 STA 线程启动（SteinsGateX/Program.cs 示例）
```

## 目录索引

| 目录 | 内容 |
|---|---|
| Core/ | Scene、GameObject、Component、Transform、InputManager、CanvasScaler、SceneManager |
| Components/ | 全部引擎组件（见上表） |
| Rendering/ | WpfRenderer、WpfEngineHost、ScenePreviewHost、ColorConversion |
| Coroutine/ | 协程调度器 + Yields 集合 |
| Scripting/ | VNS 脚本语言（Lexer/Parser/Compiler/Engine）+ 命令注册表 |
| AssetDatabase/ | 资源加载与缓存 |
| Audio/ | NAudio 封装（BGM/SFX/语音） |
| Animation/ | Ease 缓动函数 |
| Localization/ | 本地化（LocalizationManager） |
| Save/ | 存档 |
| Debugging/ | 调试控制台、DevPanel、Inspector/SceneTree 标签、UnityExplorer 复刻（UE/） |
| Diagnostics/ | Logger |
| Drawing/ | 纯 C# Color/Vector2 |

## 配套项目

- `NanoUintEditor`：可视化场景编辑器（见 `../NanoUintEditor/README.md`），
  支持「▶ 播放」经 `ScenePreviewHost` 内嵌实时预览、一键生成 `ScreenBase` C# 代码。
- `SteinsGateX`：基于本引擎的游戏项目（Screen/ 目录存放各屏幕实现）。
