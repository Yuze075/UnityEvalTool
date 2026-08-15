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

Project authorization is configured under **Project Settings > YuzeToolkit >
UnityEvalTool**. Its source of truth is
`Assets/Resources/UnityEvalToolAuthorizationSettings.asset`. Verification is off when the
asset does not exist or `RequireToken` is false. Applying a token creates a random salt and
stores only the `PBKDF2-HMAC-SHA256-v1` verifier; the original token is never serialized into
the project.

## Player

`UnityBrokerRuntimeBootstrap` creates a hidden `DontDestroyOnLoad` runner in non-Editor
builds. It reports runtime heartbeat/play state, registers the executable folder as the
project path, and publishes `Exiting` on application quit. Unity includes the authorization
asset in Player builds through its standard Resources pipeline, and the runtime loads it by
resource name without a separate post-build copy or generated file. The installed user
service is still responsible for hosting the Broker.

This is an intentional production contract, not an Editor-only or Development Build
fallback: supported release Players register and accept the same arbitrary-JavaScript eval
requests. The optional UnityAgentTool UI package is independent from this UnityEvalTool
runtime client. Verification is disabled by default. When enabled, the Player registers as
`Pending`, hashes candidate tokens supplied by the Broker with its embedded salt, and accepts
commands only after one verifier matches. The Broker stores raw candidates on the local
computer, but never makes the authorization decision. This protects against ordinary access
without the token; it does not protect against an attacker capable of patching the Player
binary. Projects embedding this package must preserve the selected contract unless they
deliberately fork the product design.

WebGL is not a supported Broker target because the local ClientWebSocket/current-user
service model is unavailable there.

## Public runtime surface

- `UnityBrokerClient.Shared.IsConnected`
- `UnityBrokerClient.Shared.AuthorizationState`
- `UnityBrokerClient.Shared.Identity`
- `UnityBrokerClient.Shared.GetSessionSnapshots("mcp:")`
- `UnityBrokerClient.Shared.GetSessionSnapshots("cli:")`
- `UnityBrokerClient.Shared.Stop()` / `Start()` for an explicit reconnect

UnityAgentTool consumes the shared Broker client for its Command Line and Eval settings
pages. It does not own service discovery, process launch, or a
separate listener.
