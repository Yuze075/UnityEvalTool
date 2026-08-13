# Editor and Player registration

**English** | [简体中文](RUNTIME_SERVICES_zh.md) | [Package README](../README.md)

UnityEvalTool has no Unity-hosted MCP or CLI listener. The computer-level Broker must be
installed and running. Editor and supported non-WebGL Player processes register outbound
to it.

## Editor

The primary Editor process starts `UnityBrokerClient` automatically. Asset Import Workers
are rejected by `EditorProcessGuard`. If the Broker cannot be reached, Unity reads
`~/.unityevaltool/install.json` and attempts to launch the installed native executable.

Compilation and assembly reload status is captured by `EditorBrokerStatusMonitor`. The
client publishes `Reloading` before Domain Reload, disconnects, and reconnects with the
same process instance ID and a higher VM generation.

## Player

`UnityBrokerRuntimeBootstrap` creates a hidden `DontDestroyOnLoad` runner in non-Editor
builds. It reports runtime heartbeat/play state, registers the executable folder as the
project path, and publishes `Exiting` on application quit. The installed user service is
still responsible for hosting the Broker.

This is an intentional production contract, not an Editor-only or Development Build
fallback: supported release Players register and accept the same authenticated arbitrary
JavaScript eval requests. Removing the optional UnityDebugTool UI package does not remove
this UnityEvalTool runtime client. Trust is bounded to the local user's loopback Broker and
its user-only token; projects embedding this package must preserve that contract unless
they deliberately fork the product design.

WebGL is not a supported Broker target because the local ClientWebSocket/current-user
service model is unavailable there.

## Public runtime surface

- `UnityBrokerClient.Shared.IsConnected`
- `UnityBrokerClient.Shared.Identity`
- `UnityBrokerClient.Shared.GetSessionSnapshots("mcp:")`
- `UnityBrokerClient.Shared.GetSessionSnapshots("cli:")`
- `UnityBrokerClient.Shared.Stop()` / `Start()` for an explicit reconnect

DebugTool's Runtime Console consumes the shared Broker client for its Command Line,
EvalTool, and Tools tabs. It does not own service discovery, process launch, or a
separate listener.
