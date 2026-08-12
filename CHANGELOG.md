# Changelog

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
