# Unity package changelog

The repository-level [CHANGELOG](../../../CHANGELOG.md) is the canonical release history
for the Unity package, computer-level Broker/CLI, npm packages and Roslyn generator.

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
