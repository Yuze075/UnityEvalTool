# UnityDebugTool

Runtime UI Toolkit debug panel for Unity projects using `UnityEvalTool`.

[Repository overview](../../README.md) · [中文](README_zh.md)

## Installation

Install `com.yuzetoolkit.unityevaltool` first, then add this package with Unity Package Manager's **Add package from git URL** command:

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unitydebugtool#main
```

For a repository embedded at `Game/UnityEvalTool`, use the local working tree:

```json
"com.yuzetoolkit.unitydebugtool": "file:../Game/UnityEvalTool/Packages/com.yuzetoolkit.unitydebugtool"
```

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
  - `Command Line`: embedded UnityEvalTool command input and output history.
  - `EvalTool`: UnityEvalTool enable, reconnect, Broker registration, Unity state, and evaluation availability.
  - `Tools`: complete runtime tool catalog with paths, descriptions, sources, functions, parameters, sub tools, and independent enable controls.
- Runtime Console uses a resizable UI Toolkit shell. Its tabs do not use UI Toolkit `ScrollView`; overflowing tab content is handled by the package's lightweight pan view with mouse-wheel panning and a compact custom scrollbar.

Default module toggles: `F10` shows or hides Performance and System Info, `F11` shows or hides Debug Windows, and `F12` shows or hides Runtime Console. `Ctrl` / `Alt` modifier requirements are still configured on `DebugPanel`; modules assigned to the same key are shown and hidden together.

`DebugPanel` is not created automatically. Place this package's `Runtime/Core/Prefabs/DebugPanel.prefab` in a scene or persistent prefab. Static `DebugWindowModule.RegisterWindow(...)` calls can happen before that prefab instance exists, but UI and EvalTool roots are activated only while the component is present and enabled. Window registrations stay registered across `DebugPanel` host teardown and Editor Play Mode transitions until their returned `IDisposable` handle is disposed.

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
```

Assembly boundaries:

- `UnityDebugTool`: Core assembly in `Runtime/Core`. Depends on `Unity.InputSystem`; does not depend on `UnityEvalTool`.
- `UnityDebugTool.DebugWindows`: debug window builder/module and EvalTool adapter. Depends on `UnityEvalTool`.
- `UnityDebugTool.Performance`: top-right FPS/RAM/audio HUD module. Does not depend on `UnityEvalTool`.
- `UnityDebugTool.SystemInfo`: bottom-right system information HUD module and public registry API. Does not depend on `UnityDebugTool.Performance` or `UnityEvalTool`.
- `UnityDebugTool.RuntimeConsole`: Runtime Console Core. Depends only on `UnityDebugTool`.
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

## Basic Usage

```csharp
using YuzeToolkit;

public sealed class PlayerDebugHandle
{
    private int _hp = 10;
    private bool _invincible;
    private readonly IDisposable _registration;

    public PlayerDebugHandle()
    {
        _registration = DebugWindowModule.RegisterWindow(
            "PlayerDebug",
            "Runtime player debug controls.",
            window =>
            {
                window.SetTitle("Player");
                window.AddValue(
                    "HP",
                    () => _hp,
                    value => _hp = value,
                    "Hp",
                    "Read or set the player's debug HP.");
                window.AddValue(
                    "Invincible",
                    () => _invincible,
                    value => _invincible = value,
                    "Invincible",
                    "Read or set player invincibility.");
                window.AddButton(
                    "Kill",
                    () => _hp = 0,
                    "Kill",
                    "Set player HP to zero.");
            });
    }

    public void Dispose()
    {
        _registration.Dispose();
    }
}
```

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

When a window is registered with `toolName` and `description`, fields and buttons that also provide `toolName` and `description` become sub tools.

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

## Notes

- This package does not depend on `com.annulusgames.debug-ui`, `com.anupackages.debugconsole`, `com.tayx.graphy`, or the project-local old `DebugPanel`.
- Runtime UI is template-driven where structure is fixed; this package's `Runtime/Core/Prefabs/DebugPanel.prefab` must be placed to enable the panel.
- `DebugPanel` requires a configured `UIDocument.panelSettings`; missing prefab configuration is treated as an error instead of being silently patched at runtime.
- Built-in field UI supports `bool`, numeric primitives, `string`, enums, `Vector2/3/4`, `Vector2Int/3Int`, `Rect/RectInt`, and `Bounds/BoundsInt`. These types can use `AddField<T>`, `AddReadOnly<T>`, or `AddValue<T>` and render with the matching UI Toolkit field. Other value types still work through EvalTool and render as read-only text in the window.
