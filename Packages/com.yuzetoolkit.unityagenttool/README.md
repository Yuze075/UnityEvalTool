# UnityAgentTool

**English** | [简体中文](README_zh.md)

UnityAgentTool is the shared Editor and Runtime workbench for Unity 2022.3. It depends on
`com.yuzetoolkit.unityevaltool` and owns the reusable UI, runtime panel host, DebugPanel lifecycle,
DebugWindow builder API, Agent conversations, Command Line sessions and Unity log viewer.

## Workbench

The Editor menu **YuzeToolkit > Unity Agent** and the runtime `UnityAgentPanelModule` both create the
same `UnityAgentWorkbenchView`. Its main sidebar has exactly five primary actions:

1. **New conversation** opens an unpersisted draft; its document is created only on first send.
2. **New command line** opens an unpersisted draft; its transcript and process-local VM start on first run.
3. **Debug Panel** displays every runtime `DebugWindowModule.RegisterWindow(...)` registration as a tab.
   Its shell remains available in Edit Mode, but runtime-owned pages are only instantiated while Play Mode is active.
4. **Log** captures Unity logs continuously from Editor domain initialization or runtime startup, independently of
   whether the Log page has been opened. It provides search, type filters, repeat grouping, clear, auto-scroll,
   Stack Trace level, Editor source navigation, local log-file access, a scrollable detail pane and a draggable
   list/detail splitter. Long list rows stay width-bounded and use a one-line summary; the selected entry renders
   its full message in a highlighted card and each stack frame as an individually readable source-aware row.
5. **System Info** displays responsive Agent-styled performance and system cards, while the standalone Runtime overlays preserve their original styling.

Agent and Command Line sessions are listed separately, keep independent input drafts, and support
pinning, archiving and deletion. Archived items leave the main workspace and are restored or deleted
from two separate Settings pages. Settings has six real pages: providers, combined configuration,
Eval connection, Eval Tools, archived conversations and archived command lines. Model discovery warnings
stay inline in the provider page instead of opening repeated dialogs, and all owned choice menus clamp to
the workspace viewport and scroll when their provider, profile or model catalog is long.

## Persistence

All non-secret machine settings live at `Application.persistentDataPath/settings.json` with fixed folders:

```text
AgentConversations/       Agent conversation documents
CommandLineHistory/       Command Line documents and selected-session state
```

Command Line input, output and drafts survive Unity restarts. JavaScript `EvalSession` instances do not.
Provider secrets stay in the machine-local `secrets.json`. Provider-free defaults in
`Assets/Resources/UnityAgentProjectSettings.json` are included in Player builds and seed a missing or
invalid machine settings file.

## Runtime Host

`DebugPanel`, now fully owned by this package, owns one full-screen `UIDocument` and drives `IDebugPanelModule` lifetimes and toggle
keys. `UnityAgentPanelModule` is the unified F8 workspace. Its header drags the whole window; the
upper-right handle resizes width and height freely inside the panel bounds. Collapse hides the full
content and resize hit area, releases focus, and remains independent from System Info visibility.
The window is bottom-left anchored and its geometry is persisted with `PlayerPrefs`. This package also
supplies the normal composition prefab and the protected System Info / Performance views. The dependency direction is:

```text
UnityAgentTool -> UnityEvalTool
```

The separate UnityDebugTool package no longer exists.

## DebugWindow API

DebugWindow registration moved into this package but keeps the `YuzeToolkit` namespace:

```csharp
var handle = DebugWindowModule.RegisterWindow(window =>
{
    window.SetTitle("Player");
    window.AddReadOnly("State", () => player.StateName);
    window.AddPrimaryButton("Reset", player.Reset);
});
```

Registrations do not require a scene host. `DebugWindowModule` only registers visual windows; it never
creates, registers, or disposes an `IEvalTool`. Feature owners implement automation independently and
register its lifetime through `EvalToolRegistry.RegisterRootScoped`. `AddButton` is a neutral action,
`AddPrimaryButton` is the page's primary action, and `AddPreviousButton` / `AddNextButton` are directional
actions. Default boolean, enum, foldout, range, and progress controls use the Agent palette and package-owned
interaction styling instead of Unity's default skin.

## Assemblies

- `UnityAgentTool`: Agent core, all shared UI, DebugPanel, DebugWindow API, Command Line and Log.
- `UnityAgentTool.Editor`: EditorWindow and Editor Broker settings bridge.

The package does not expose the old Runtime Console registry, tab-provider assemblies, Eval runtime
page, compatibility providers or DebugWindow MonoBehaviour host.
