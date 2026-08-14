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

Conversation rendering shows User and Assistant text, pending approval cards, and every Tool call as a
collapsed transcript row. Expanding a Tool row reveals its arguments and pending, successful, or failed result.
Tool messages remain fully persisted and are still sent back to the model. The workbench inherits the active Unity PanelSettings / Theme font; it does not bundle, enumerate,
dynamically create or explicitly assign a font.

## Agent loop

The built-in HTTP Agent uses a deliberately small sequential loop: one model response is persisted, each
tool call receives exactly one ordered result, and the model continues until it returns no tool calls. A
turn has a configurable model-step limit (64 by default). Cancellation, an unexpected failure, or a Unity
Domain Reload closes every uncompleted tool call with an explicit error result before the terminal state is
saved, so a later turn never receives an orphaned tool protocol.

Provider profiles store the model context window. When an HTTP conversation approaches that limit, the
complete transcript remains in its conversation document while a semantic summary checkpoint plus the
latest complete message boundary is projected to the model. Transient HTTP network errors, 429 responses,
and recoverable 5xx responses are retried at most twice and only before the first SSE event; partial model
output is never retried.

The built-in Editor and Runtime system prompts are English. They define the Unity role, name the actual file,
process, shell, Skill, and `unity_eval_js` entry Tools, and tell the model which one to call first—including
starting unfamiliar Unity work by discovering `tools://` modules. Exact arguments and detailed execution
contracts remain in the structured Tool schemas sent with every model request.

## Standalone Agent boundary

The Agent loop, sessions, approvals, context compaction, Tool dispatch and `unity_eval_js` execution all run
inside the Unity process. The default host directly creates the HTTP model Provider and an in-process
UnityEvalTool `EvalExecutor`; it does not start or connect to Codex, a Broker, MCP or the computer-level CLI.
The separate Eval connection page manages optional external access to UnityEvalTool and is not an Agent runtime
dependency. Process and shell Tools start a requested program only when the model explicitly calls them.

OpenAI models use the OpenAI Responses API with an API key. A ChatGPT/Codex subscription is not an embeddable
Provider credential, so UnityAgentTool does not read Codex login caches or expose Codex App Server. Existing
`codex-app-server` profiles are migrated to the standard OpenAI API preset and require `OPENAI_API_KEY` or a
locally saved API key.

In Editor, active conversations are paused when script compilation starts. The package writes a
process- and project-bound recovery marker to `Application.persistentDataPath`, interrupts and persists the
turn, then appends one continuation message after a successful Domain Reload or a failed compilation. The
continuation includes compiler counts and tells the Agent to re-inspect Unity state because cached Unity
objects and the JavaScript VM do not survive a reload. A marker from another Editor process is discarded
instead of automatically running work after a restart.

## Persistence

All non-secret machine settings live at `Application.persistentDataPath/settings.json` with fixed folders:

```text
AgentConversations/       Agent conversation documents
CommandLineHistory/       Command Line documents and selected-session state
UnityAgentEditorCompilationRecovery.json  Editor-only active-turn recovery marker
```

Command Line input, output and drafts survive Unity restarts. JavaScript `EvalSession` instances do not.
Provider secrets stay in the machine-local `secrets.json`. Provider-free defaults in
`Assets/Resources/UnityAgentProjectSettings.json` are included in Player builds. When—and only when—the current
Editor or Player has no machine `settings.json`, the complete machine configuration is created from these
defaults. An existing or malformed machine file is never replaced by Project Settings. Edit the defaults
through **Edit > Project Settings > YuzeToolkit > Unity Agent**; the page covers permission, Editor/Runtime
prompts, Tool limits, and ordered AGENTS.md/Skill roots. Editor Play Mode uses the Editor prompt; the Runtime
prompt is reserved for standalone Players.

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
