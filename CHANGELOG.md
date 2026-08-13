# Changelog

**English** | [简体中文](CHANGELOG_zh.md)

## Unreleased

- Rebuild the public documentation as matching English and Simplified Chinese guides,
  make the repository README the complete installation and first-use entry point, separate
  reproducible source packaging from maintainer-defined distribution, and remove
  host-project-specific development instructions.
- Preserve npm metadata lookup failures during artifact preflight and pass tarballs through
  build checks as explicit local paths.
- Pin .NET SDK 10.0.300, exclude source-control revisions from the SourceGenerator
  assembly, and regenerate the committed analyzer so byte-for-byte validation is stable
  across repository layouts.
- Correct committed-version evaluation in build automation before package validation.
- Prepare version `2.0.2`: UnityEvalTool, Broker, and npm packages use 2.0.2;
  UnityDebugTool 1.0.1 depends on UnityEvalTool 2.0.2.
- Make multi-package artifact validation SHA-bound, concurrency-safe, smoke-tested,
  version-preflighted, and recoverable when an immutable artifact already exists.
- Store the committed Unity analyzer as a normal Git blob so UPM Git installs and binary
  validation receive the actual DLL.
- Remove install/uninstall lifecycle dependence: service setup and removal are explicit,
  checked `unity service install|uninstall` steps around global npm install/uninstall.
- Keep the inherited copyright notice and current copyright consistently in the repository
  and both UPM package license files.
- Return Unity eval output as native MCP text/image blocks with the correct top-level error
  bit instead of nesting CallToolResult-shaped JSON as structured text.
- Serialize commands per Unity connection: queued cancellation and timeout no longer send,
  while an interrupted sent command remains explicitly outcome-unknown and blocks later
  execution until resolved.
- Make cold-start auth-token publication cross-process atomic, bound unauthenticated first
  frames and connection count, and bound every WebSocket close path before aborting an
  unresponsive peer.
- Validate complete JavaScript Tool trees, callable sub-tool resolvers, explicit safety
  flags, and non-reserved export names before registration; add durable-data risk metadata
  and owner-aware root removal.
- Diagnose unsupported nested, asynchronous, and JavaScript-reserved C# Eval functions at
  generation time and make Roslyn integration tests independent of their output directory.
- Rework UnityDebugTool registration rollback, input focus, bounded logs, recursive Tool
  catalog, performance buffers, and IL2CPP preservation. Visual layout metadata no longer
  creates implicit Eval Tools; callers use an explicit Tool tree.
- Preserve authenticated arbitrary JavaScript eval in supported non-WebGL Release Players
  as an intentional runtime contract independent of the optional UnityDebugTool UI.
- Add `com.yuzetoolkit.unitydebugtool` under `Packages` so the runtime debug UI and
  UnityEvalTool share one source repository while retaining package-specific READMEs.
- Keep MCP/CLI executable in `CompilationFailed` repair mode through the last successful
  Unity assemblies while continuing to reject compile/import/reload transitions.
- Clarify event-driven compilation waits and same-process handle reuse across registry
  changes, and add Broker state-policy regression tests.

## 2.0.1 - 2026-08-12

- Release Unity-side PuerTS sessions when CLI consoles close or Broker leases expire,
  including deferred release across a temporary Unity disconnect.
- Isolate Unity Broker client connection generations so a stopped reconnect loop cannot
  tear down a newly started connection.
- Detect Broker process replacement and reset Unity-side sessions that belonged to the
  previous Broker.
- Replace the self-sustaining Editor player-loop wakeup with a throttled status heartbeat
  driven by normal Editor updates.
- Keep Broker, Unity package, npm, and runtime versions synchronized from the committed
  `version.json` version.
- Store the Roslyn generator as ordinary repository source in `Roslyn` instead of embedding
  a source archive in the Unity package.

## 2.0.0 - 2026-08-12

- Replace Unity-hosted MCP and CLI listeners with a computer-level C# NativeAOT Broker on
  `127.0.0.1:2347`.
- Add authenticated registration and state reporting for multiple Unity Editor and Player
  processes.
- Add explicit compilation, assembly reload, import, play-mode transition, disconnection,
  and main-thread-stall states.
- Reduce the MCP surface to `unity_status`, `unity_connect`, and `eval`, with mandatory
  discovery and selection before execution.
- Add event-driven readiness and compilation-completion waits that continue across Unity
  Domain Reload.
- Add the native `unity` CLI with project-path auto-selection, instance selection, one-shot
  commands, and an interactive console that reuses Unity's existing parser.
- Add current-user service integration for macOS LaunchAgent, Linux systemd user units, and
  Windows Scheduled Tasks.
- Add npm packaging for macOS, Linux, and Windows on x64 and arm64, with a six-platform
  artifact build matrix.
- Move the Unity Package Manager package to `Packages/com.yuzetoolkit.unityevaltool` and the
  Broker source to `Broker`.

This is a protocol and distribution breaking release. Remove legacy UnityCLI installations
and configure MCP clients for the authenticated port 2347 endpoint.
