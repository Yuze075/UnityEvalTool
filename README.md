# UnityEvalTool

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-222?logo=unity)](https://unity.com/releases/editor/archive)
[![npm](https://img.shields.io/badge/npm-%40yuzetoolkit%2Funityevaltool-CB3837?logo=npm)](https://www.npmjs.com/package/@yuzetoolkit/unityevaltool)
[![Broker](https://img.shields.io/badge/Broker-127.0.0.1%3A2347-4b7bec)](Broker/README.md)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

[中文说明](README_zh.md) · [UnityEvalTool package](Packages/com.yuzetoolkit.unityevaltool/README.md) · [UnityDebugTool package](Packages/com.yuzetoolkit.unitydebugtool/README.md) · [Broker protocol](Packages/com.yuzetoolkit.unityevaltool/docs/BROKER_PROTOCOL.md)

UnityEvalTool connects every local Unity Editor or Player to one computer-level Broker. AI agents and terminal users talk to the Broker instead of depending on a listener inside a Unity script domain. Compilation, assembly reload, process exit and a stalled Unity main thread therefore remain visible even while eval is temporarily unavailable; failed compilation keeps MCP/CLI available in repair mode through the last successfully loaded assemblies.

The repository also ships UnityDebugTool, a UI Toolkit runtime debug panel and console that exposes the same tool model to players, developers and AI agents.

## Repository layout

```text
UnityEvalTool
├── Packages/
│   ├── com.yuzetoolkit.unityevaltool/   # Broker client, MCP eval and CLI runtime
│   └── com.yuzetoolkit.unitydebugtool/  # Runtime debug UI and console
├── Broker/
│   ├── src/                             # C# NativeAOT Broker and CLI
│   └── npm/                             # npm entry and native platform packages
├── Roslyn/                              # Source generator solution and tests
└── .github/workflows/
    └── release.yml                      # Six-platform build and release matrix
```

Each package owns its package-specific setup and API documentation. This README covers the combined repository, Broker and release workflow.

## Installation

### 1. Install the Unity packages

#### UnityEvalTool

UnityEvalTool requires `com.tencent.puerts.core` 3.0.2 and exactly one PuerTS JavaScript backend. This repository is validated with `com.tencent.puerts.quickjs` 3.0.2; alternatively use one supported V8 backend/core pair from the same PuerTS release. Do not install multiple backends at once. Then add UnityEvalTool with Unity Package Manager's **Add package from git URL** command:

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.2
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.yuzetoolkit.unityevaltool": "https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.2"
  }
}
```

When this repository is embedded at `Game/UnityEvalTool` in a Unity project, consume the
working tree directly instead of copying the package:

```json
"com.yuzetoolkit.unityevaltool": "file:../Game/UnityEvalTool/Packages/com.yuzetoolkit.unityevaltool"
```

#### UnityDebugTool

Repository tag `v2.0.2` contains UnityDebugTool package version `1.0.1`, which depends on
UnityEvalTool `2.0.2`. Install UnityEvalTool first, then add the optional runtime debug UI package:

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unitydebugtool#v2.0.2
```

For an embedded development checkout, reference both working-tree packages:

```json
"com.yuzetoolkit.unitydebugtool": "file:../Game/UnityEvalTool/Packages/com.yuzetoolkit.unitydebugtool",
"com.yuzetoolkit.unityevaltool": "file:../Game/UnityEvalTool/Packages/com.yuzetoolkit.unityevaltool"
```

UnityDebugTool usage, prefab setup, modules and APIs are documented in its [package README](Packages/com.yuzetoolkit.unitydebugtool/README.md).

### 2. Install the Broker and CLI

```bash
npm install --global @yuzetoolkit/unityevaltool
unity service install
unity doctor
```

The npm package installs the native `unity` executable for macOS, Linux or Windows on x64
and arm64. Service setup is an explicit second step because modern npm versions may block
dependency lifecycle scripts. `unity service install` creates and starts a current-user
service, never a system-wide privileged daemon. Check that it succeeds before running
`unity doctor`; do not disable npm's install-script security policy.

## CLI quick start

```bash
unity list
unity                         # Select the Unity matching the current directory
unity connect <instance-id>   # Enter that Unity's interactive console
unity Runtime getState        # Run one existing Unity-side CLI command
unity eval-js --code "return 1 + 2;"
unity service status
```

The interactive console keeps the existing Unity-side command parser. Broker commands are `:status`, `:wait`, `:switch`, `:help` and `:quit`.

## MCP configuration

The Streamable HTTP endpoint is `http://127.0.0.1:2347/mcp`. The first install creates `~/.unityevaltool/auth.json`. Configure the MCP client to send its `token` as:

```text
Authorization: Bearer <token>
```

The server exposes only three tools:

1. `unity_status` discovers Unity instances and can wait for readiness or a new compilation cycle to finish.
2. `unity_connect` selects an exact `instanceId` from a known registry revision and returns a workflow-local opaque handle.
3. `eval` executes in the selected Unity. It rejects calls made before discovery and selection, and rejects calls while Unity cannot safely eval.

Do not retry an interrupted mutating `eval`: the response explicitly reports when execution may already have occurred. See the [protocol specification](Packages/com.yuzetoolkit.unityevaltool/docs/BROKER_PROTOCOL.md) for the state model, errors and wait semantics.

## Service management

```bash
unity service install
unity service status
unity service start
unity service stop
unity service restart
unity service uninstall
```

The service uses a LaunchAgent on macOS, a systemd user unit on Linux, and a current-user Scheduled Task on Windows. The Broker binds loopback port 2347 only and fails explicitly if that port is already owned by another process.

To uninstall, remove the current-user service while the `unity` executable still exists,
verify that this first command succeeds, and only then remove the npm package. npm does not
run uninstall lifecycle scripts:

```bash
unity service uninstall
npm uninstall --global @yuzetoolkit/unityevaltool
```

## Development and release

```bash
dotnet build Broker/UnityEvalTool.Broker.slnx -c Release
dotnet test Roslyn/UnityEvalToolRoslyn.sln -c Release
cd Broker
node --input-type=module -e "import { resolveAndValidateVersion } from './npm/scripts/version.mjs'; console.log(resolveAndValidateVersion(process.cwd()));"
node npm/scripts/pack-platform.mjs
node npm/scripts/pack-root.mjs
```

Broker, Roslyn and both Unity packages are ordinary source folders in this repository. No
source archive is required and package development does not download an auxiliary zip.
`version.json` is the release version authority; CI verifies the Broker, Unity package,
runtime constant and npm package metadata before building six NativeAOT platform packages
plus the platform-independent npm entry package. Publishing is deliberately gated behind
an explicit workflow input and npm credentials. See [Broker/README.md](Broker/README.md)
for package internals and the release checklist.

When this tree is embedded under `Game/UnityEvalTool` in RelicLight, ordinary RelicLight
contributors publish only the parent RelicLight repository to CNB. They do not need this
repository's GitHub remote, GitHub write access or a second checkout. Only a machine that
maintains both repositories explicitly enables the parent repository's mirror hooks; those
hooks reconcile the complete tree and publish GitHub only after CNB has accepted the exact
RelicLight source commit. A GitHub failure is retained as recoverable pending state and is
never reported as though the already-successful CNB update had been rolled back.

## License

[MIT](LICENSE)
