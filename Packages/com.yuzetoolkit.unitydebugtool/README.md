# UnityDebugTool

**English** | [简体中文](README_zh.md) | [Repository guide](../../README.md)

UnityDebugTool is the downstream System Info and Performance extension for UnityAgentTool. The
DebugPanel host, DebugWindow API, Log, Command Line and unified workbench now live in UnityAgentTool.

## Dependency And Runtime Composition

The package depends on `com.yuzetoolkit.unityagenttool`; it never depends on UnityEvalTool directly.
Place `Runtime/Core/Prefabs/DebugPanel.prefab` in a scene. The prefab composes:

- `DebugPanel` and `UnityAgentPanelModule` from UnityAgentTool;
- `SystemInfoModule` from `UnityDebugTool.SystemInfo`;
- `PerformanceMonitorModule` from `UnityDebugTool.Performance`.

F8 toggles the unified Unity Agent workspace. F10 toggles the standalone System Info and Performance
overlay together. Their visibility and the Agent window's collapsed state are independent.

## System Info Integration

System Info remains available as a standalone overlay with its existing behavior. Both modules also
register workbench sections through `UnityAgentWorkspaceRegistry`:

1. System Info rows are displayed first.
2. Performance FPS/RAM/audio metrics are displayed below them.

The protected files below retain their previous visual design and are shared by standalone and
embedded instances:

- `Runtime/SystemInfo/UI/SystemInfo.uxml` and `SystemInfo.uss`
- `Runtime/Performance/UI/PerformanceMonitor.uxml` and `PerformanceMonitor.uss`

`SystemInfoRegistry.Register(key, Func<string>)` and `Unregister(key)` remain the public API for
project-specific rows.

## Assemblies

- `UnityDebugTool.SystemInfo`: registry, snapshots, view and module; references `UnityAgentTool`.
- `UnityDebugTool.Performance`: sampler, snapshots, graph view and module; references `UnityAgentTool`.
- `UnityDebugTool.Editor`: contributes both protected views to the Agent workbench outside PlayMode.

There is no Debug core assembly, DebugWindow assembly, Runtime Console assembly or compatibility
layer in this package.
