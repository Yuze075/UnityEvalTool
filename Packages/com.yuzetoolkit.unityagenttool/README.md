# UnityAgentTool

**English** | [简体中文](README_zh.md)

UnityAgentTool is an in-process Unity Agent package for Unity 2022.3. It runs its Agent loop, tools, conversations, permissions, provider adapters, and native JavaScript evaluation inside Unity. It does not connect through UnityEvalTool Broker, MCP, or the UnityEvalTool CLI.

## Install

Install these packages together:

- `com.yuzetoolkit.unityevaltool` 2.0.2
- `com.yuzetoolkit.unitydebugtool` 1.0.1
- this package, `com.yuzetoolkit.unityagenttool`

For a local checkout, select this package's `package.json` with Package Manager's **Add package from disk**. The Agent assemblies reference `UnityEvalTool` directly and do not reference `UnityEvalTool.Broker`, `UnityEvalTool.CLI`, or the Broker Editor assemblies. An already installed Broker may keep running independently, but it is not an Agent prerequisite.

## User Interfaces

The Editor exposes one `Unity Agent` workbench. All three menu items target that same
`EditorWindow`; `Open` and `Chat` select the conversation page, while `Settings` selects the
settings page:

- **YuzeToolkit > Unity Agent > Open**
- **YuzeToolkit > Unity Agent > Chat**
- **YuzeToolkit > Unity Agent > Settings**

Runtime Console receives one **Unity Agent** tab through `RuntimeConsoleTabRegistry`. Its
workbench contains both Chat and Settings, so switching pages never creates a second console tab
or `UIDocument`. A scene must still contain UnityDebugTool's configured
`Runtime/Core/Prefabs/DebugPanel.prefab`; this package does not create a `DebugPanel` automatically.

The left sidebar shows compact conversation rows. Conversations can remain ungrouped or be moved
into user-defined groups, reordered by drag and drop, pinned, archived, and deleted from their
context menu. The lower-left Settings button changes the current workbench page. Operational
failures and confirmations use a centered modal overlay instead of an easy-to-miss footer message.

The workbench owns its complete UI Toolkit appearance. Native text editing remains available
for IME, selection, and clipboard behavior, but buttons, fields, integer input, toggles,
dropdowns, model menus, context menus, tooltips, scrollbars, and every interaction state are
drawn by package controls instead of Unity's default skin.

The composer owns only conversation choices: permission mode, provider profile, model, and
reasoning effort. Its single action button sends when text is ready; if a turn is active it first
stops and drains that turn, then sends the captured text to the same conversation. With empty input
it only stops the active turn. Workspace and system prompt are deliberately absent: every conversation is
locked to the current Editor project root or built Player project root, and the global system prompt
is edited only in Settings.

The complete configuration is always stored at
`Application.persistentDataPath/.unityagenttool/settings.json`; Unity UI and external editors modify
the same file. **Reload from disk** explicitly applies external changes. One portable path setting
selects the conversation-history root and defaults to `PersistentData + .unityagenttool`; individual
documents live in its `Sessions` child directory. Portable paths persist a stable base plus a
relative path, never a machine-specific absolute path. On first upgrade, settings and history are
copied non-destructively from the old Editor `Library/UnityAgentTool` or Player
`persistentDataPath/UnityAgentTool` location; source files remain untouched. API keys may be kept
for the current process or explicitly saved to
`Application.persistentDataPath/.unityagenttool/secrets.json`. The secret file is separate from
`settings.json`, is restricted to the current user on macOS and Linux, is never rendered back into
the UI, and is never written to provider profiles, transcripts, or packaged project content. A
persisted profile may also name an environment variable as the lowest-priority secret source.

## Providers

Provider profiles use one normalized Agent contract and currently support:

- OpenAI Responses API
- OpenAI-compatible Chat Completions API
- Anthropic Messages API
- Google Gemini Interactions API
- Codex App Server over a local JSONL process

Chat and Settings automatically try the provider's remote model-list endpoint the first time each
profile is shown, and discovery can also be refreshed manually. Model selection is never a free-form
text field: remote discovery is authoritative when available, and a maintained catalog supplies
selection-only fallback values, provider defaults, model limits, and supported reasoning choices
when discovery fails. The catalog includes presets for OpenAI, Anthropic,
Google, xAI, Meta, Kimi/Moonshot, GLM/Z.AI, Qwen, MiniMax, MiMo, and DeepSeek, plus separate Qwen
international and China endpoints. Remote discovery remains authoritative because vendor catalogs
change over time.

For HTTP protocols, **Base URL** is the HTTP API root. For `codex-app-server`, the same stored field is presented as **Codex executable** and contains either an executable name or an absolute path; the default is `codex`. Codex starts `codex app-server --stdio` and uses the existing local Codex login, so API-key fields are disabled for that protocol.

## Agent Tools And Permissions

The built-in Agent tools cover file and directory operations, local processes and platform shells, Skill discovery/read, and direct Unity JavaScript evaluation. JavaScript runs through UnityEvalTool's in-process `EvalExecutor` with one persistent eval session per Agent conversation.

The Agent loop has no configurable or persisted `MaxSteps` quota. It continues across model and
tool turns until the model finishes, the user stops it, the provider fails or times out, a tool
reports a real failure, or another runtime boundary ends the turn. Context compaction remains a
request/storage boundary rather than a tool-call limit: it physically replaces old messages with a
bounded summary only at complete assistant/tool boundaries and also bounds the retained tail.

Permission modes:

- `FullAccess` executes registered tools without a UI approval pause.
- `ConfirmWrites` pauses every non-read-only tool and renders an Approve/Decline card in chat.

Treat direct Unity JavaScript and local process execution as high-privilege capabilities. Only use `FullAccess` with a trusted model endpoint and trusted instructions.

## AGENTS.md, Skills, And Player Builds

AGENTS.md and Skills use two independent ordered path lists. Each item contains a stable base (`ProjectRoot`, `PersistentData`, `UserProfile`, `Documents`, local/roaming application data, temporary cache, or `StreamingAssets`), an optional relative path, and an **Include in Player build** switch; absolute relative-path values are rejected. Defaults are explicit, removable, build-enabled `ProjectRoot / .` AGENTS and `ProjectRoot / .agents/skills` entries. Newly added external paths default to Editor-only so personal files cannot be shipped accidentally.

The Editor reads every entry in priority order, independent of its build switch. Players never read the host's live configured locations: the build emits separate ordered manifests from enabled entries in the two lists. If a default ProjectRoot entry is removed or build-disabled, its corresponding project content is not packaged implicitly.

The build processor copies only configured instruction content to a generated `Temp` staging directory and registers it with Unity 2022's `BuildPlayerContext.AddAdditionalPathToStreamingAssets`. It never writes generated content into `Assets`, does not import external Skill files into AssetDatabase, rejects symbolic links, skips Unity `.meta` files, and enforces per-file, total-size, and file-count limits. The staging directory is removed after the build and before the next build.

Do not add a build root until it has been reviewed for credentials, private documents, large assets, and instructions that should not ship to end users.

## Platform Notes

- Local process and shell tools require a desktop-style platform with `System.Diagnostics.Process` support.
- WebGL cannot run local processes. Browser networking also requires the provider endpoint to allow CORS.
- Mobile and console platforms generally should not expose a shell.
- `StreamingAssets` is a normal filesystem path on desktop Players. Android and WebGL require a platform-aware packaged-content reader before bundled AGENTS/Skill loading can be considered supported there.
- PuerTS backend availability still determines whether direct Unity JavaScript evaluation is available on a target.

## Runtime Console Registry

Other packages may add process-wide Runtime Console tabs without modifying the DebugPanel prefab:

```csharp
private static IDisposable? registration;

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void RegisterTabs()
{
    registration = RuntimeConsoleTabRegistry.Register(
        "com.example.my-tabs",
        context => new IRuntimeConsoleTab[] { new MyRuntimeConsoleTab(context) });
}
```

Register before `RuntimeConsoleModule` initializes. Factory ids are unique, factories are evaluated again when the console host is reinitialized, and disposing the returned handle removes the factory from future host initializations.
