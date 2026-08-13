# UnityEvalTool package

**English** | [简体中文](README_zh.md) | [Repository guide](../../README.md)

`com.yuzetoolkit.unityevaltool` is the Unity-side half of UnityEvalTool. It registers each
supported Editor or Player with the computer-level Broker, reports lifecycle state, hosts
persistent PuerTS eval sessions, exposes helper modules, and preserves the existing
Unity-side CLI command grammar.

Install and first-use instructions for both the Unity package and the required Broker/CLI
are in the [repository guide](../../README.md). This page describes the Unity package
itself.

## Requirements

- Unity 2022.3 or newer.
- `com.tencent.puerts.core` 3.0.2.
- Exactly one compatible PuerTS JavaScript backend. The tested combination is
  `com.tencent.puerts.quickjs` 3.0.2 with matching core 3.0.2; a supported V8/core pair
  from the same PuerTS release may be used instead.
- The `@yuzetoolkit/unityevaltool` Broker/CLI installed and running on the computer.

Do not install multiple PuerTS backends in one Unity project.

## Add the package

Use Unity Package Manager's **Add package from git URL** command:

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.2
```

For a local source checkout, use **Add package from disk** and select this package's
`package.json`.

## Editor lifecycle

The primary Editor process starts its Broker client automatically after script loading.
Asset Import Workers are excluded. Open **YuzeToolkit > UnityEvalTool** to inspect and
control registration for the current process. The window reports the installed Broker,
connection state, Unity phase, eval availability, compilation counters, and registered
tool catalog.

The Editor reports importing, compilation, compilation failure, assembly reload, play-mode
transition, and main-thread responsiveness independently of eval. During Domain Reload,
the Broker retains the Unity instance and valid selection handles; the same process
reconnects with a new connection epoch and VM generation. A failed compilation keeps the
last successfully loaded assemblies available as repair mode.

## Player lifecycle and security

Supported non-WebGL Players start a hidden `DontDestroyOnLoad` Broker client and register
using the executable directory as their project path. Release Players intentionally retain
the same authenticated arbitrary-JavaScript eval surface as the Editor. This is not gated
by Development Build and is independent of the optional UnityDebugTool package.

The trust boundary is the current user's loopback Broker and per-user authentication token.
If that capability is not appropriate for a shipped product, exclude or alter the package
as an explicit product decision. WebGL is not a supported Broker target.

Lifecycle details and public connection APIs are documented in
[Editor and Player registration](docs/RUNTIME_SERVICES.md).

## MCP execution contract

The computer-level Broker exposes `unity_status`, `unity_connect`, and `eval`. The `eval`
tool executes a program with this shape inside the selected Unity session:

```javascript
async function execute() {
  const runtime = await import('tools://Runtime');
  return runtime.getState();
}
```

Use `tools://` to discover root modules and `tools://<Tool/Path>` to import a module. The
built-in roots are `Runtime`, `Runtime/Objects`, `Runtime/Components`,
`Runtime/Diagnostics`, `Runtime/Inspect`, `Runtime/Reflection`, `Runtime/ObserveFrames`,
`UnityEval`, and the Editor-only `Editor` hierarchy. The Editor hierarchy includes direct
viewport images, persistent Unity Test Framework runs, and bounded serialized code-usage
search. Prefer these semantic helpers over direct `CS.*` interop.

- [Helper module reference](docs/HELPER_MODULES.md)
- [Advanced sessions, compilation, and errors](docs/ADVANCED_USAGE.md)
- [Broker protocol](docs/BROKER_PROTOCOL.md)
- [Architecture](docs/PROJECT_DESIGN.md)

## Extending the tool catalog

Define a partial C# class with `[EvalTool]`, mark exported methods with `[EvalFunction]`,
and let the bundled Roslyn analyzer generate its `IEvalTool` metadata. Every function must
declare an explicit safety level. Tool registration validates paths, callable sub-tools,
JavaScript export names, parameters, and safety metadata before making a module visible.

Loader-backed JavaScript tools can be registered through `EvalToolRegistry` and inspected
or enabled through `tools://UnityEval`. Use `getJsToolAuthoringPrompt()` on that module for
the current authoring contract. Return JSON-serializable primitives, lists, dictionaries,
or data composed from those types whenever possible.

## Fixed local endpoints

| Endpoint | Purpose |
|---|---|
| `http://127.0.0.1:2347/health` | Broker health |
| `http://127.0.0.1:2347/mcp` | MCP Streamable HTTP |
| `ws://127.0.0.1:2347/unity` | Authenticated Unity registration and routing |
| `ws://127.0.0.1:2347/cli` | Authenticated native CLI consoles |

The Broker binds loopback only, authenticates every connection with the current user's
token, and fails explicitly if port `2347` is unavailable.

## Build from source

This UPM package is consumed directly from the `Packages/com.yuzetoolkit.unityevaltool`
source directory; there is no separate Unity-package archive script. The native Broker and
npm packaging sources live at the repository root. See the
[Broker build guide](../../Broker/README.md) and [Roslyn generator guide](../../Roslyn/README.md).

## License

[MIT](LICENSE)
