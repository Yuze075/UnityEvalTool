# UnityEvalTool 2

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-222?logo=unity)](https://unity.com/releases/editor/archive)
[![npm](https://img.shields.io/badge/npm-%40yuzetoolkit%2Funityevaltool-CB3837?logo=npm)](https://www.npmjs.com/package/@yuzetoolkit/unityevaltool)
[![Broker](https://img.shields.io/badge/Broker-127.0.0.1%3A2347-4b7bec)](Broker/README.md)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

[中文说明](README_zh.md) · [Unity package](Packages/com.yuzetoolkit.unityevaltool/README.md) · [Broker protocol](Packages/com.yuzetoolkit.unityevaltool/docs/BROKER_PROTOCOL_2.md)

UnityEvalTool connects every local Unity Editor or Player to one computer-level Broker. AI agents and terminal users talk to the Broker instead of depending on a listener inside a Unity script domain. Compilation, assembly reload, process exit and a stalled Unity main thread therefore remain visible even while eval is temporarily unavailable.

## Repository layout

```text
UnityEvalTool
├── Packages/
│   └── com.yuzetoolkit.unityevaltool/   # Unity Package Manager package
├── Broker/
│   ├── src/                             # C# NativeAOT Broker and CLI
│   └── npm/                             # npm entry and native platform packages
└── .github/workflows/
    └── release.yml                      # Six-platform build and release matrix
```

## Installation

### 1. Install the Unity package

UnityEvalTool requires `com.tencent.puerts.core` 3.0.0 and one PuerTS JavaScript backend. Add the package with Unity Package Manager's **Add package from git URL** command:

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.0
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.yuzetoolkit.unityevaltool": "https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.0"
  }
}
```

### 2. Install the Broker and CLI

```bash
npm install --global @yuzetoolkit/unityevaltool
unity doctor
```

The npm package installs the native `unity` executable and a current-user background service. It does not install a system-wide privileged daemon. Supported targets are macOS, Linux and Windows on both x64 and arm64.

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

Do not retry an interrupted mutating `eval`: the response explicitly reports when execution may already have occurred. See the [protocol specification](Packages/com.yuzetoolkit.unityevaltool/docs/BROKER_PROTOCOL_2.md) for the state model, errors and wait semantics.

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

## Development and release

```bash
dotnet build Broker/UnityEvalTool.Broker.slnx -c Release
cd Broker
node npm/scripts/pack-platform.mjs
node npm/scripts/pack-root.mjs
```

`release.yml` builds six NativeAOT platform packages plus the platform-independent npm entry package. Publishing is deliberately gated behind an explicit workflow input and npm credentials. See [Broker/README.md](Broker/README.md) for package internals and the release checklist.

## License

[MIT](LICENSE)
