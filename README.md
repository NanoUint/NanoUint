# NanoUint

A tiny WPF-based visual novel engine ("A Nano Unity Game Engine for Visual Novels based on WPF").
Built for the *Steins;Gate X* fan project: a Unity-style object/component/coroutine model, WPF rendering, and a script command system.

## Architecture Overview

```
┌────────────────────────────────────────────────────────────┐
│ Game project (e.g. SteinsGateX)                            │
│   ScreenBase subclasses · Chapter coroutines · game logic  │
└───────────────┬────────────────────────────────────────────┘
┌───────────────▼────────────────────────────────────────────┐
│ NanoUint (this library)                                    │
│  Core        Scene / GameObject / Component / Transform    │
│              InputManager / CanvasScaler / SceneManager    │
│  Components  SpriteRenderer / TextRenderer / buttons /     │
│              dialogue / choices / background / audio /     │
│              effects / timeline (Tweener)                  │
│  Rendering   WpfRenderer (incremental) /                   │
│              WpfEngineHost (main loop) /                   │
│              ScenePreviewHost (editor preview)             │
│  Coroutine   Scheduler + Yields collection                 │
│  Scripting   VNS parse/compile/execute + command registry  │
│  AssetDatabase  Asset loading (filesystem) + cache         │
│  Audio       NAudio wrapper (BGM/SFX/voice)                │
│  Animation   Ease functions                                │
│  Localization  Multi-language                              │
│  Save        Save data                                     │
│  Debugging   DebugConsole / DevPanel / Inspector / UE      │
└────────────────────────────────────────────────────────────┘
```

## Core Concepts

### Scenes and Objects (Unity-style)

```csharp
var scene = new Scene("Main");
var go = scene.AddObject("Logo");          // triggers Awake automatically
go.Transform.X = 0.5f;                     // normalized coords: 0=left/top, 1=right/bottom
go.Transform.Y = 0.5f;
go.Transform.SortingOrder = 10;            // Z depth, higher draws in front
go.Transform.Opacity = 1f;
go.Transform.SetParent(parent.Transform);  // hierarchy (world coords accumulate from parents)
```

- `GameObject`: a container with no behavior of its own; compose it with `AddComponent<T>()`
- `Component`: a behavior unit (Awake/Start/Update/OnDestroy); `Behaviour` adds coroutine support
- `Transform`: required on every object; X/Y are 0-1 normalized coords relative to the parent, SortingOrder decides draw order

### Component List (Components/)

| Component | Purpose |
|---|---|
| `SpriteRenderer` | Image display (Sprite/Tint/mouth animation MouthClosed/Half/Open) |
| `TextRenderer` | Plain text (Content/FontSize/TextColor) |
| `SpriteButton` / `PassiveButton` | Image button / hit-area button (OnClick/OnEnter/OnExit); SpriteButton attaches a PassiveButton automatically |
| `BackgroundRenderer` | Full-screen background + two-texture crossfade (CrossfadeTo) |
| `DialogueBox` | Dialogue (SpeakerName/Show/typewriter ProgressiveReveal/TextSpeed) |
| `ChoiceGroup` | Choice group (Show/Inline/WaitForChoiceAsync) |
| `AudioSource` | Audio (Clip/IsLooping/Volume/Play/Stop/Pause) |
| `FlashOverlay` | Full-screen color flash |
| `AdvanceIndicator` | "Continue reading" indicator |
| `LineRenderer` | Line segments (normalized coords, SetPositions) |
| `HintRenderer` | Hint text |
| `Tweener` | Timeline tweening (works with Animation/Ease) |
| `BacklogView` / `PhoneScreen` / `Slider` / `VideoPlayer` | Backlog / phone UI / slider / video |

### Coroutines

```csharp
StartCoroutine(MyRoutine());
IEnumerator MyRoutine()
{
    yield return new WaitForSeconds(0.5f);
    yield return new WaitForClick();
    // WaitForChoice / WaitUntil / WaitWhile / TypewriterDelay ...
}
```

### Script Commands (Scripting/)

```csharp
ScriptEngine engine = ...;
engine.Registry.Register(new MyCommand());   // IScriptCommand
engine.Run("chapter1.vns");                  // VNS parse -> compile -> execute
```

### Assets (AssetDatabase/)

```csharp
AssetDatabase.AddSearchDirectory(@"C:\...\Resources");
var sprite = AssetDatabase.Load<Sprite>("Characters/okabe.png", 100f); // ppu: pixels per world unit
var audio  = AssetDatabase.Load<AudioClip>("BGM/theme.ogg");
```

### Rendering and Hosts (Rendering/)

- `WpfRenderer`: incremental rendering (only dirty components are synced); CanvasScaler handles resolution adaptation
- `WpfEngineHost`: the full game host (Window + main loop + input + UE debug panels), internal
- `ScenePreviewHost` (public): **for editor/external embedding** — renders the engine into any Canvas,
  with a manually driven frame loop and 1920x1080 logical canvas scaling:

```csharp
var host = new ScenePreviewHost(canvas, 1920, 1080);
// Add GameObjects/components to host.Scene (or let NanoUintEditor.SceneRuntime build them)
host.StartScene();   // fires Start
host.Start();        // starts the frame loop
// ...
host.Stop();         // stop on shutdown
```

## Game Integration (IGameBootstrapper)

Implement `IGameBootstrapper` and start the host from your entry point:

```csharp
public class MyGame : IGameBootstrapper
{
    public void OnStart()
    {
        AssetDatabase.AutoDetect();
        SceneManager.LoadScene(new Scene("Main"));
    }
}
// WpfEngineHost.Run starts on an STA thread (see SteinsGateX/Program.cs)
```

## Directory Index

| Directory | Contents |
|---|---|
| Core/ | Scene, GameObject, Component, Transform, InputManager, CanvasScaler, SceneManager |
| Components/ | All engine components (see the table above) |
| Rendering/ | WpfRenderer, WpfEngineHost, ScenePreviewHost, ColorConversion |
| Coroutine/ | Coroutine scheduler + Yields collection |
| Scripting/ | VNS scripting language (Lexer/Parser/Compiler/Engine) + command registry |
| AssetDatabase/ | Asset loading and caching |
| Audio/ | NAudio wrapper (BGM/SFX/voice) |
| Animation/ | Ease functions |
| Localization/ | Localization (LocalizationManager) |
| Save/ | Save data |
| Debugging/ | Debug console, DevPanel, Inspector/SceneTree tabs, UnityExplorer replica (UE/) |
| Diagnostics/ | Logger |
| Drawing/ | Pure C# Color/Vector2 |

## Companion Projects

- `NanoUintEditor`: visual scene editor (see `../NanoUintEditor/README.md`); supports live embedded
  preview via `ScenePreviewHost` and one-click generation of `ScreenBase` C# code.
- `SteinsGateX`: the game built on this engine (screen implementations live in `Screen/`).
