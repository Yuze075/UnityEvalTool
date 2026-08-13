# Changelog

## Unreleased

- Added `com.yuzetoolkit.unitydebugtool` under `Packages` so the runtime debug UI and UnityEvalTool share one source repository while retaining package-specific READMEs.
- Keep MCP/CLI executable in `CompilationFailed` repair mode through the last successful Unity assemblies, while continuing to reject compile/import/reload transitions.
- Clarify event-driven compilation waits and same-process handle reuse across registry changes, and add Broker state-policy regression tests.

## 2.0.1 - 2026-08-12

- Release Unity-side PuerTS sessions when CLI consoles close or Broker leases expire, including deferred release across a temporary Unity disconnect.
- Isolate Unity Broker client connection generations so a stopped reconnect loop cannot tear down a newly started connection.
- Detect Broker process replacement and reset Unity-side sessions that belonged to the previous Broker.
- Replace the self-sustaining Editor player-loop wakeup with a throttled status heartbeat driven by normal Editor updates.
- Keep Broker, Unity package, npm and runtime versions synchronized from the committed `version.json` release version.
- Store the Roslyn generator as ordinary repository source in `Roslyn` instead of embedding a source zip in the Unity package.

## 2.0.0 - 2026-08-12

- Replaced Unity-hosted MCP and CLI listeners with a computer-level C# NativeAOT Broker on `127.0.0.1:2347`.
- Added authenticated registration and state reporting for multiple Unity Editor and Player processes.
- Added explicit compilation, assembly reload, import, play-mode transition, disconnection and main-thread-stall states.
- Reduced the MCP surface to `unity_status`, `unity_connect` and `eval`, with mandatory discovery and selection before execution.
- Added event-driven readiness and compilation-completion waits that continue across Unity domain reloads.
- Added the native `unity` CLI with project-path auto-selection, instance selection, one-shot commands and an interactive console that reuses Unity's existing parser.
- Added current-user service integration for macOS LaunchAgent, Linux systemd user units and Windows Scheduled Tasks.
- Added npm packaging for macOS, Linux and Windows on x64 and arm64, plus a six-platform GitHub Actions release workflow.
- Moved the Unity Package Manager package to `Packages/com.yuzetoolkit.unityevaltool` and the Broker source to `Broker`.

This is a protocol and distribution breaking release. Remove legacy UnityCLI installations and configure MCP clients for the authenticated port 2347 endpoint.
