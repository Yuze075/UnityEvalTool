# UnityAgentTool

**English** | [简体中文](README_zh.md)

UnityAgentTool is the shared Editor and Runtime workbench for Unity 2022.3. It depends on
`com.yuzetoolkit.unityevaltool` and owns the reusable UI, runtime panel host, DebugPanel lifecycle,
DebugWindow builder API, Agent conversations, Command Line sessions and Unity log viewer.

## Workbench

The Editor menu **YuzeToolkit > Unity Agent** and the runtime `UnityAgentPanelModule` both create the
same `UnityAgentWorkbenchView`. Its main sidebar has exactly five primary actions:

1. **New conversation** creates an Agent conversation.
2. **New command line** creates a persistent Command Line transcript; its process-local VM starts lazily.
3. **Debug Panel** displays every `DebugWindowModule.RegisterWindow(...)` registration as a tab.
4. **Log** captures Unity logs with search, type filters, repeat grouping, clear, auto-scroll,
   Stack Trace level, Editor source navigation and local log-file access.
5. **System Info** displays sections contributed by downstream packages.

Agent and Command Line sessions are listed in separate sidebar groups. Settings is a separate,
full-workspace page. Providers, Agent defaults, instructions, history and Eval Tool are real pages;
navigation switches page roots instead of scrolling one long document. At widths below 1024 px the
settings navigation becomes a 56 px icon rail.

## Persistence

Settings remain at `Application.persistentDataPath/.unityagenttool/settings.json`. The configured
history root contains:

```text
Sessions/                 Agent conversation documents
CommandLineSessions/      Command Line documents and selected-session state
```

Command Line input and output survive Unity restarts. JavaScript `EvalSession` instances never do:
each selected transcript lazily creates a fresh VM in the current Unity process.

## Runtime Host

`DebugPanel` owns one full-screen `UIDocument` and drives `IDebugPanelModule` lifetimes and toggle
keys. `UnityAgentPanelModule` is the unified F8 workspace. Its header drags the whole window; the
upper-right handle resizes width and height freely inside the panel bounds. Collapse hides the full
content and resize hit area, releases focus, and remains independent from System Info visibility.
Geometry is persisted with `PlayerPrefs`.

UnityDebugTool supplies the normal composition prefab and the protected System Info / Performance
views. The package dependency direction is:

```text
UnityDebugTool -> UnityAgentTool -> UnityEvalTool
```

Agent never references Debug.

## DebugWindow API

DebugWindow registration moved into this package but keeps the `YuzeToolkit` namespace:

```csharp
var handle = DebugWindowModule.RegisterWindow(window =>
{
    window.SetTitle("Player");
    window.AddReadOnly("State", () => player.StateName);
    window.AddButton("Reset", player.Reset);
});
```

Registrations do not require a scene host. Explicit `DebugEvalToolBuilder` roots are registered with
`EvalToolRegistry` immediately and are removed with the returned handle. Visual controls are created
with the Agent palette and owned interaction styling; the former DebugWindows USS is not reused.

## Assemblies

- `UnityAgentTool`: Agent core, all shared UI, DebugPanel, DebugWindow API, Command Line and Log.
- `UnityAgentTool.Editor`: EditorWindow and Editor Broker settings bridge.

The package does not expose the old Runtime Console registry, tab-provider assemblies, Eval runtime
page, compatibility providers or DebugWindow MonoBehaviour host.
