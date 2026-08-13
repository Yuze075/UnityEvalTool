# UnityEvalTool

UnityEvalTool lets AI agents and terminal users operate Unity through one computer-level
Broker. Unity no longer hosts an MCP listener or a separate CLI port. Each Editor or
Player registers one authenticated WebSocket with the Broker; MCP and CLI operations are
routed through that connection and execute through the existing PuerTS eval/tool system.

[中文](README_zh.md) · [Protocol](docs/BROKER_PROTOCOL.md) · [Helper modules](docs/HELPER_MODULES.md)

## Components

- Unity Package Manager package `com.yuzetoolkit.unityevaltool`: registration client,
  compilation/reload status monitor, PuerTS eval sessions, helper tools, and Unity-side
  CLI command parser.
- npm package `@yuzetoolkit/unityevaltool`: installs the native `unity` command and a
  current-user background service bound to `127.0.0.1:2347`.
- Native platform packages: macOS, Windows, and Linux on x64 and arm64. JavaScript is
  only the npm launcher; the Broker and CLI are C# NativeAOT.

## Install

UnityEvalTool requires `com.tencent.puerts.core` and exactly one PuerTS backend. This
repository is validated with `com.tencent.puerts.quickjs` 3.0.2 and its matching core
3.0.2; a supported V8 backend/core pair from the same PuerTS release is an alternative.
Add this Unity package through Package Manager, then install the computer-level package:

```bash
npm install --global @yuzetoolkit/unityevaltool
unity service install
unity doctor
```

Use this Git URL in Unity Package Manager:

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.2
```

Service installation is deliberately explicit because modern npm versions may block
dependency lifecycle scripts. `unity service install` installs and starts a current-user
LaunchAgent (macOS), systemd user unit (Linux), or Scheduled Task (Windows). It never
requires an administrator service. Use
`unity service status|start|stop|restart|uninstall` for explicit management.

The Broker creates `~/.unityevaltool/auth.json` with user-only permissions. MCP clients
connect to `http://127.0.0.1:2347/mcp` and must send its token as
`Authorization: Bearer <token>`. Unity and CLI authenticate with the same local token.

## MCP workflow

The Broker exposes exactly three tools:

1. `unity_status`: list all Unity processes and their state. It can wait for `ready` or
   `compilation-complete` by `instanceId` before selection, or by `connectionHandle`
   afterward. Both waits may return `CompilationFailed`; always inspect `phase`,
   `canEval`, and compiler counts.
2. `unity_connect`: select the exact `instanceId` using the `registryRevision` returned
   by the preceding status snapshot. It returns an opaque, workflow-scoped handle.
3. `eval`: execute the existing `async function execute() { ... }` contract in the
   selected Unity. It requires the handle and is rejected while Unity is busy.

Agents must complete status and connect before eval. Handles are not globally selected,
survive registry changes and Domain Reload for the same Unity process, expire when idle,
and are invalidated if that process is replaced. Registry changes alone do not require a
new handle. An interrupted eval is never retried automatically.

Unity reports `Ready`, `Importing`, `Compiling`, `CompilationFailed`, `Reloading`,
`PlayModeTransition`, and exit/connectivity state independently of eval. This lets an
agent wait in the Broker even while Unity's scripting domain does not exist. When
compilation fails, eval and CLI remain available in repair mode through the last
successfully loaded assemblies so the agent can read errors, edit code, and refresh again.

## CLI

```bash
unity list
unity                         # auto-select by current project path, enter a console
unity connect <instance-id>   # select explicitly, enter a console
unity Runtime getState        # auto-select and execute once
unity connect <id> -- Editor getCompilationState
unity eval-js --code "return 1 + 2;"
```

Inside the interactive console, `:status`, `:wait`, `:switch`, `:help`, and `:quit` are
Broker commands. Every other line is forwarded unchanged to Unity's
`EvalCliCommandService`, preserving the DebugTool and helper command workflow.

## Fixed local endpoints

| Endpoint | Purpose |
|---|---|
| `http://127.0.0.1:2347/health` | Broker health |
| `http://127.0.0.1:2347/mcp` | MCP Streamable HTTP |
| `ws://127.0.0.1:2347/unity` | Unity registration and routing |
| `ws://127.0.0.1:2347/cli` | Native CLI consoles |

The Broker binds loopback only and fails explicitly if port 2347 is occupied. It does
not expose a LAN mode or silently choose a different port.

Non-WebGL release Players intentionally keep the same authenticated arbitrary-JavaScript
eval surface as the Editor; this is not gated by Development Build and does not depend on
the optional UnityDebugTool UI package. See [Editor and Player registration](docs/RUNTIME_SERVICES.md).

## Development and release

The NativeAOT source and npm pipeline live in the repository-level `Broker` directory.
The release matrix builds six RID-specific packages and one entry package. Run the
current-platform packaging checks from that directory; publishing is a separate explicit
action.
