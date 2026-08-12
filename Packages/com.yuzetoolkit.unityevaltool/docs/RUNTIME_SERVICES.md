# Editor and Player registration

UnityEvalTool 2 has no Unity-hosted MCP or CLI listener. The computer-level Broker must be
installed and running. Both Editor and non-WebGL Player processes register outbound to it.

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

WebGL is excluded because it cannot use the required local ClientWebSocket/service model.

## Public runtime surface

- `UnityBrokerClient.Shared.IsConnected`
- `UnityBrokerClient.Shared.Identity`
- `UnityBrokerClient.Shared.GetSessionSnapshots("mcp:")`
- `UnityBrokerClient.Shared.GetSessionSnapshots("cli:")`
- `UnityBrokerClient.Shared.Stop()` / `Start()` for an explicit reconnect

DebugTool Runtime Console service and conversation tabs consume this surface. They no
longer start listeners; they show Broker registration and routed PuerTS sessions.
