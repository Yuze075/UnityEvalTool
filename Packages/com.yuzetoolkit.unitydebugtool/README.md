# UnityDebugTool

**English** | [简体中文](README_zh.md) | [Repository guide](../../README.md)

Runtime UI Toolkit debug panel for Unity projects using `UnityEvalTool`.

## Installation

Install `com.yuzetoolkit.unityevaltool` first, then add this package with Unity Package Manager's **Add package from git URL** command:

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unitydebugtool#v2.0.2
```

For a local source checkout, use Package Manager's **Add package from disk** command and
select `Packages/com.yuzetoolkit.unitydebugtool/package.json` inside that checkout.

Place `Runtime/Core/Prefabs/DebugPanel.prefab` in a scene or persistent prefab to enable the UI. The package does not create a panel automatically.

## What It Provides

- Scene-placed runtime `DebugPanel`.
- Top-left debug windows registered through static builder APIs; registrations are cached while no `DebugPanel` instance exists.
- Optional dynamic `IEvalTool` registration for AI-friendly runtime control once a `DebugPanel` instance is enabled.
- Built-in performance HUD:
  - FPS, frame time, average FPS, 1% low average, 0.1% low average.
- Built-in system information HUD:
  - Screen, window, graphics API, GPU, VRAM, max texture size, shader level, CPU, RAM, OS.
  - Runtime registration through `SystemInfoRegistry.Register(key, Func<string>)`.
- Registered Runtime Console tabs:
  - `Log`: Unity Console-style log toolbar, filters, collapse, list, and detail view.
  - `Command Line`: an in-process, persistent, single-line UnityEvalTool CLI session and output history.
  - `EvalTool`: UnityEvalTool enable, reconnect, Broker registration, Unity state, and evaluation availability.
  - `Tools`: the recursively flattened runtime tool catalog with every path, function, parameter, safety declaration, and effective enable state.
- Runtime Console uses a resizable UI Toolkit shell. Its tabs do not use UI Toolkit `ScrollView`; overflowing tab content is handled by the package's lightweight pan view with mouse-wheel panning and a compact custom scrollbar.

Default module toggles: `F10` shows or hides Performance and System Info, `F9` shows or hides Debug Windows, and `F8` shows or hides Runtime Console. `Ctrl` / `Alt` modifier requirements are still configured on `DebugPanel`; modules assigned to the same key are shown and hidden together.

`DebugPanel` is not created automatically. Place this package's `Runtime/Core/Prefabs/DebugPanel.prefab` in a scene or persistent prefab. Keep exactly one active `DebugPanel` / `DebugWindowModule` host per process; duplicate active hosts fail initialization explicitly so one Tool root never has ambiguous ownership. Static `DebugWindowModule.RegisterWindow(...)` calls can happen before that prefab instance exists, but UI and EvalTool roots are activated only while the component is present and enabled. Window registrations survive a `DebugPanel` host teardown inside the same managed runtime until their returned `IDisposable` handle is disposed. A normal Unity Domain Reload resets static registrations, so their owners must register again through their normal initialization lifecycle.

Debug Windows are pointer-oriented. Buttons, foldouts, sliders, numeric fields, and window background do not acquire keyboard or gamepad focus; numeric fields remain adjustable with their pointer-drag affordance, while unsupported object-like values render read-only. A writable string field accepts keyboard input only after a left mouse click inside that exact field; Enter, clicking elsewhere, changing tabs, or hiding the panel ends editing and clears the EventSystem selection. Runtime Console search and Command Line fields use the same explicit mouse-entry rule. This policy prevents retained UI focus from turning later Submit/navigation input into an accidental debug action; it cannot by itself disable independent gameplay `InputAction` callbacks, so host projects that require modal text entry should gate their gameplay action map while a console text field is active.

## Runtime Structure

The panel root is intentionally thin:

- `DebugPanel` owns `UIDocument` setup, root visibility, toggle input, and module lifetime.
- `IDebugPanelModule` implementations expose their own `ToggleKey` and receive a `DebugPanelContext`, not the panel object. Modules create their own layers and load their own USS through that context.
- `DebugWindowModule` owns debug-window hosting and delegates static registration to `DebugWindowRegistry`.
- `PerformanceMonitorModule` only coordinates `PerformanceSampler` and a UXML-backed `PerformanceMonitorView`.
- `SystemInfoModule` only renders `SystemInfoRegistry` entries through its own UXML/USS.
- `RuntimeConsoleModule` only hosts tabs returned by prefab-mounted `IRuntimeConsoleTabProvider` components. If no providers are present, it creates no console UI.

Current runtime layout:

```text
Runtime/
  Core/
    UnityDebugTool.asmdef
    DebugPanel, module interface, context, UI Toolkit template helper
    Prefabs/        DebugPanel prefab
    Settings/       PanelSettings asset and default Unity theme import
  DebugWindows/
    UnityDebugTool.DebugWindows.asmdef
    builder API, node model, EvalTool adapter, window registry/module
    UI/             debug window USS
  Performance/
    UnityDebugTool.Performance.asmdef
    sampler, snapshots, HUD view, graph element, module
    UI/             performance UXML and USS
  SystemInfo/
    UnityDebugTool.SystemInfo.asmdef
    registry, snapshots, HUD view, module
    UI/             system information UXML and USS
  RuntimeConsole/
    Core/
      UnityDebugTool.RuntimeConsole.asmdef
      module, tab provider interface, tab view, base USS
    Log/
      UnityDebugTool.RuntimeConsole.Log.asmdef
      Unity Console-style log tab
    CliRepl/
      UnityDebugTool.RuntimeConsole.CliRepl.asmdef
      UnityEvalTool command line tab
    EvalTool/
      UnityDebugTool.RuntimeConsole.EvalTool.asmdef
      UnityEvalTool state and control tab
    Tools/
      UnityDebugTool.RuntimeConsole.Tools.asmdef
      runtime tool catalog and control tab
Tests/Editor/       registration, safety, catalog, and bounded-log contract tests
link.xml            IL2CPP preservation for reflection-invoked Debug Eval Tool adapters
```

Assembly boundaries:

- `UnityDebugTool`: Core assembly in `Runtime/Core`. Depends on `Unity.InputSystem` and Unity EventSystem APIs for root-level focus cleanup; it does not depend on `UnityEvalTool`.
- `UnityDebugTool.DebugWindows`: debug window builder/module and EvalTool adapter. Depends on `UnityEvalTool`, Input System, and Unity EventSystem APIs.
- `UnityDebugTool.Performance`: top-right FPS/RAM/audio HUD module. Does not depend on `UnityEvalTool`.
- `UnityDebugTool.SystemInfo`: bottom-right system information HUD module and public registry API. Does not depend on `UnityDebugTool.Performance` or `UnityEvalTool`.
- `UnityDebugTool.RuntimeConsole`: Runtime Console Core. Depends on `UnityDebugTool`, Input System, and Unity EventSystem APIs for focus cleanup.
- `UnityDebugTool.RuntimeConsole.Log`: Unity log tab. Depends on Runtime Console Core.
- `UnityDebugTool.RuntimeConsole.CliRepl`: Command Line tab. Depends on `UnityEvalTool` and `UnityEvalTool.CLI`.
- `UnityDebugTool.RuntimeConsole.EvalTool`: UnityEvalTool state and connection controls. Depends on `UnityEvalTool.Broker`.
- `UnityDebugTool.RuntimeConsole.Tools`: complete tool catalog and independent controls. Depends on `UnityEvalTool`.

## UXML And USS

Static UI structure belongs in UXML when the structure is fixed. Runtime Console tabs are registered views and build their runtime-only UI in C# because their rows and service cards are data-driven.

Current templates:

- `Runtime/Performance/UI/PerformanceMonitor.uxml`: FPS/RAM/audio HUD structure.
- `Runtime/SystemInfo/UI/SystemInfo.uxml`: system information HUD structure.
- Runtime Console no longer uses a single UXML template. Its Core creates the tab container, and prefab-mounted tab providers create each tab.
- Runtime Console tabs intentionally avoid UI Toolkit `ScrollView`. Use `RuntimeConsoleUi.CreatePanView()` for wheel-panned overflowing runtime content with the built-in custom scrollbar.

Styles are owned by the module that uses them. Core does not provide a shared root USS. `Runtime/Core/Settings/DebugPanelDefaultTheme.tss` imports only Unity's built-in default theme so UI Toolkit controls can render.

- `Runtime/DebugWindows/UI/DebugWindows.uss`: registered debug-window UI.
- `Runtime/Performance/UI/PerformanceMonitor.uss`: FPS/RAM/audio HUD.
- `Runtime/SystemInfo/UI/SystemInfo.uss`: system information HUD.
- `Runtime/RuntimeConsole/Core/UI/RuntimeConsoleCore.uss`: Runtime Console tab shell.
- `Runtime/RuntimeConsole/Log/UI/RuntimeLogTab.uss`: Unity Console-style log tab.
- `Runtime/RuntimeConsole/CliRepl/UI/RuntimeCliReplTab.uss`: Command Line tab.
- `Runtime/RuntimeConsole/EvalTool/UI/RuntimeEvalToolTab.uss`: EvalTool state and control tab.
- `Runtime/RuntimeConsole/Tools/UI/RuntimeToolsTab.uss`: Tools catalog tab.

Built-in modules and Runtime Console tab providers receive their USS through serialized prefab references. No runtime UI asset is loaded through `Resources`. `DebugPanel` only clears and exposes the `UIDocument` root, then drives module lifecycle.

A built-in module or tab provider is active only when its `MonoBehaviour` is enabled. Missing required UXML/USS references fail that initialization explicitly. If any module throws while initializing, `DebugPanel` shuts down already-started modules in reverse order and does not retry every frame.

## Recommended Explicit Visual And Tool Trees

```csharp
using System;
using YuzeToolkit;

public sealed class PlayerDebugHandle : IDisposable
{
    private int _hp = 10;
    private bool _invincible;
    private readonly IDisposable _registration;

    public PlayerDebugHandle()
    {
        var tool = new DebugEvalToolBuilder(
            "PlayerDebug",
            "Stable runtime player debug controls.");
        tool.AddWritable("Hp", "Read or set the player's HP.", () => _hp, value => _hp = value)
            .AddWritable("Invincible", "Read or set invincibility.", () => _invincible,
                value => _invincible = value)
            .AddButton("Kill", "Set player HP to zero.", () => _hp = 0,
                EvalToolSafety.Destructive | EvalToolSafety.RequiresConfirmation);

        _registration = DebugWindowModule.RegisterWindow(
            tool,
            window =>
            {
                window.SetTitle("Player");
                window.AddSegmentedInt("HP", 0, 10, () => _hp, value => _hp = value);
                window.AddBoolButton("Invincible", () => _invincible, value => _invincible = value);
                window.AddButton("Kill", () => _hp = 0);
            });
    }

    public void Dispose()
    {
        _registration.Dispose();
    }
}
```

The visual tree owns layout; the explicit `DebugEvalToolBuilder` owns stable automation paths. Both must reuse the same getters, setters, and actions, but a Foldout or horizontal layout never decides Tool identity. With this overload, visual node `toolName` metadata is ignored. Dispose the returned handle when the represented runtime owner goes away; disposal removes both the window and its exact owned root Tool.

The legacy `RegisterWindow(toolName, description, configure)` overload is obsolete. It now creates a visual-only window and keeps the supplied name only as visual metadata; it never registers an Eval Tool. Migrate every automation surface to the explicit two-tree overload. Visual builder `toolName` / `description` arguments remain source-compatible but are ignored for Tool registration.

Windows registered without a tool name and description are UI-only:

```csharp
DebugWindowModule.RegisterWindow(window =>
{
    window.SetTitle("Local Debug");
    window.AddReadOnly("State", () => "Ready");
});
```

System information entries are one row per registered key:

```csharp
SystemInfoRegistry.Register("Player State", () => player.StateName);
SystemInfoRegistry.Unregister("Player State");
```

## EvalTool Usage

The explicit example above creates stable `PlayerDebug/Hp`, `PlayerDebug/Invincible`, and `PlayerDebug/Kill` paths regardless of visual grouping.

```javascript
async function execute() {
  const hp = await import("tools://PlayerDebug/Hp");
  const before = hp.get();
  const after = hp.set(20);

  const kill = await import("tools://PlayerDebug/Kill");
  kill.invoke();

  return { before, after };
}
```

Use `tools://` and `getToolDetails("PlayerDebug")` to inspect the full tree.

## Builder API

- `SetTitle(string title)`
- `SetDraggable(bool draggable)`
- `AddLabel(string text)`
- `AddSpace(float height = 8)`
- `AddReadOnly<T>(string label, Func<T> getter, string? toolName = null, string? description = null)`
- `AddValue<T>(string label, Func<T> getter, Action<T> setter, string? toolName = null, string? description = null)`
- `AddField<T>(string label, Func<T> getter)` / `AddField<T>(string label, Func<T> getter, Action<T> setter)` as the old debug-ui compatible field entry point.
- `AddReadOnlyBool / AddReadOnlyInt / AddReadOnlyFloat / AddReadOnlyString(...)`
- `AddBool / AddInt / AddFloat / AddString(...)`
- `AddSlider(...)` for float and int values, rendered as draggable filled sliders. Float and int sliders support both old `"{0:F2}"` composite formats and new `"0.##"` numeric formats.
- `AddProgress(...)` / `AddProgressBar(...)` for float and int values.
- `AddButton(...)`
- `AddImage(...)` for static or dynamic `Texture2D`, `Sprite`, `RenderTexture`, and `VectorImage` previews.
- `AddGroup(string label, Action<DebugGroupBuilder> configure, bool registerAsTool = true)`
- `AddGroup(string label, string toolName, string description, Action<DebugGroupBuilder> configure)`
- `AddFoldout(string label, Action<DebugGroupBuilder> configure)` as the old debug-ui compatible foldout entry point. It does not auto-register an EvalTool sub-tool.
- `AddHorizontalGroup(...)`
- `AddVerticalGroup(...)`

Tool names and descriptions must be provided together. Tool names are validated by `EvalToolRegistry`.

`DebugEvalToolBuilder` provides `AddGroup`, `AddReadOnly`, `AddWritable`, `AddButton`, and `AddDestructiveButton`. `AddWritable(..., EvalToolSafety safety)` and `AddButton(..., EvalToolSafety safety)` accept the complete UnityEvalTool safety flags; use `PersistsData` for local saves/settings, `LongRunning` for asynchronous flows, and the appropriate mutation flags for scene, project, editor, network, or reload effects. A destructive action must also declare `RequiresConfirmation`.

The embedded Command Line accepts one line per submission. Its local `help` lists only supported embedded semantics. Computer-level `unity connect`, stdin, heredoc, and external REPL exit are intentionally unavailable; `exit` reports this rather than silently ending a non-existent process.

## Notes

- This package does not depend on `com.annulusgames.debug-ui`, `com.anupackages.debugconsole`, `com.tayx.graphy`, or the project-local old `DebugPanel`.
- Runtime UI is template-driven where structure is fixed; this package's `Runtime/Core/Prefabs/DebugPanel.prefab` must be placed to enable the panel.
- `DebugPanel` requires a configured `UIDocument.panelSettings`; missing prefab configuration is treated as an error instead of being silently patched at runtime.
- Built-in field UI supports `bool`, numeric primitives, `string`, enums, `Vector2/3/4`, `Vector2Int/3Int`, `Rect/RectInt`, and `Bounds/BoundsInt`. These types can use `AddField<T>`, `AddReadOnly<T>`, or `AddValue<T>` and render with the matching UI Toolkit field. Other value types still work through EvalTool and render as read-only text in the window.
