# Unity package changelog

The repository-level [CHANGELOG](../../../CHANGELOG.md) is the canonical release history
for the Unity package, computer-level Broker/CLI, npm packages and Roslyn generator.

## Unreleased

- Prepare package version 2.0.2 and pin Git URL installation to the immutable repository tag `v2.0.2`.
- Keep the committed source-generator analyzer as a real Git blob for UPM Git installs and release validation.
- Eval results now cross the Broker as native MCP text/image/error content; per-Unity serialization prevents a queued request from executing after its caller already received a timeout.
- Tool registration recursively validates JavaScript descriptors, callable child resolution, safety flags and export identifiers. `PersistsData` describes durable non-project writes.
- Broker auth-token creation is atomic across processes, unauthenticated sockets are bounded, and stalled close handshakes are aborted after a deadline.
- Source generation reports nested Tool types, async/Task-like functions and reserved JavaScript export names before runtime registration.
- Supported non-WebGL Release Players intentionally retain authenticated arbitrary JavaScript eval independently of UnityDebugTool.
- `CompilationFailed` is now an executable repair mode backed by the last successfully
  loaded assemblies, allowing MCP/CLI to read errors, edit source, and refresh again.
- Agent guidance now waits through Broker status instead of eval polling and keeps the
  existing handle across same-process registry changes and Domain Reload.

## 2.0.1 - 2026-08-12

- PuerTS sessions now follow Broker lease and CLI-console lifetimes and are released across
  temporary Unity disconnects.
- Broker reconnect generations cannot overwrite the state of a newer connection, and a
  Broker process restart resets sessions owned by the previous process.
- Editor status publication uses a throttled normal update heartbeat without continuously
  requesting additional player-loop updates.
- Package and Broker versions are synchronized, and Roslyn generator source lives in the
  repository `Roslyn` folder rather than an embedded source zip.

## 2.0.0 - 2026-08-12

- Replaced Unity-hosted MCP/CLI listeners with one authenticated, computer-level NativeAOT
  Broker on `127.0.0.1:2347`.
- Added event-driven Unity discovery, selection handles, compilation waiting, status phases,
  native CLI service management and six-platform npm packaging.
