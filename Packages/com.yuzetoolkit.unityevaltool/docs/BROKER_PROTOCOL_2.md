# UnityEvalTool Broker Protocol 2.0

This document defines the stable boundary between the computer-level UnityEvalTool Broker and Unity clients. The Broker owns discovery, status, selection, waiting and routing. Unity owns PuerTS eval sessions, tool registration and CLI command parsing.

## Endpoints

- `http://127.0.0.1:2347/mcp`: MCP Streamable HTTP endpoint.
- `ws://127.0.0.1:2347/unity`: authenticated Unity client connection.
- `ws://127.0.0.1:2347/cli`: authenticated interactive CLI connection.
- `http://127.0.0.1:2347/health`: Broker health snapshot.

The Broker binds loopback only. It must not silently choose another port when `2347` is unavailable.

## Envelope

Unity and CLI WebSockets exchange one UTF-8 JSON object per WebSocket message:

```json
{
  "protocol": "2.0",
  "type": "request",
  "id": "globally-unique-request-id",
  "method": "eval/execute",
  "payload": {}
}
```

`type` is `request`, `response`, or `event`. A failed response has an `error` object with stable `code`, human-readable `message`, and `mayHaveExecuted` when execution outcome is uncertain.

## Unity registration

The first Unity message must be `unity/register`. Its payload contains:

- `authToken`
- `instanceId`: stable across Domain Reload for one Unity process
- `connectionEpoch`: incremented for each Unity-side connection generation
- `processId` and `processStartedAtUtc`
- `projectName`, canonical `projectPath`, `unityVersion`, `packageVersion`
- `environment`: `Editor` or `Player`
- the complete initial `status`

Only the primary Unity Editor process may register. Asset Import Workers must never register or start the Broker.

## Status

Unity publishes `unity/status` events. A status contains independent transport and main-thread observations plus:

- `phase`: `Starting`, `Ready`, `Importing`, `Compiling`, `CompilationFailed`, `Reloading`, `PlayModeTransition`, `MainThreadStalled`, `Exiting`, `Exited`, or `Incompatible`
- `canEval`
- `busyReason`
- `mainThreadTick`
- `isPlaying`, `isPaused`, `isUpdating`
- `compilationCycleId`, compiler error/warning counts and last compilation timestamps
- `vmGeneration`

The Broker determines transport connectivity itself. A live socket does not prove that the Unity main thread is responsive. When the main-thread tick is stale, the Broker derives `MainThreadStalled` even if Unity's last published phase was `Compiling` or `Reloading`; `busyReason` preserves that last reported phase.

## Broker-to-Unity requests

- `eval/execute`: `sessionId`, `requestId`, `code`, `timeoutSeconds`, `resetSession`
- `cli/execute`: `sessionId`, `requestId`, raw `line`
- `session/release`: dispose a named Unity-side eval session
- `broker/ping`: transport-level liveness check

Unity executes `eval/execute` and `cli/execute` only while `canEval` is true. The Broker never retries an interrupted mutating request automatically.

## Selection handles

There is no process-global selected Unity. `unity_connect` creates an opaque, unguessable `connectionHandle` bound to one registered `instanceId`. MCP calls and CLI consoles carry their own handle. A status snapshot returns `registryRevision`; connect must submit that revision so a stale discovery result cannot silently target a changed registry.

Handles survive a temporary Domain Reload disconnect for the same `instanceId`, but the returned status exposes the new `connectionEpoch` and `vmGeneration`. Handles expire after inactivity and become invalid when their instance exits or is replaced by a different process lifetime.

## Compilation and reload

Every observed Unity compilation receives a `compilationCycleId`, including compilations not initiated through eval. Unity publishes `Compiling` at `CompilationPipeline.compilationStarted`, compiler counts during assembly completion, `CompilationFailed` on errors, and `Reloading` before assembly reload. After reconnect, Unity publishes `Ready` only after a stable main-thread update.

`unity_status` may wait for `ready` or `compilation-complete`. Before selection it accepts an `instanceId`, so an agent that starts while Unity is compiling or temporarily disconnected can wait and then call `unity_connect` with the returned current registry revision. After selection it can wait through the opaque handle. Waiting is event-driven in the Broker and never runs inside Unity eval. `requestId` in the status tool refers to the observed `compilationCycleId`, not an eval request id. When an eval is about to trigger compilation, retain the preceding snapshot's `capturedAtUtc` and pass it as `observedAfterUtc`; the Broker then waits for a compilation that actually started after that marker instead of accepting a stale `Ready` sample.

## Stable error codes

- `AuthenticationFailed`
- `ProtocolMismatch`
- `InvalidRequest`
- `DiscoveryRequired`
- `RegistryChanged`
- `UnityNotFound`
- `ConnectionHandleRequired`
- `ConnectionHandleInvalid`
- `UnityDisconnected`
- `UnityBusy`
- `CompilationFailed`
- `RequestTimedOut`
- `ExecutionOutcomeUnknown`
- `BrokerUnavailable`
