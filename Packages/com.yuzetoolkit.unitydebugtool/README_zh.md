# UnityDebugTool

[English](README.md) | **简体中文** | [仓库说明](../../README_zh.md)

UnityDebugTool 现在是 UnityAgentTool 的下游 System Info 与 Performance 扩展包。DebugPanel 宿主、
DebugWindow API、Log、Command Line 与统一工作台已经移动到 UnityAgentTool。

## 依赖与 Runtime 组合

本包依赖 `com.yuzetoolkit.unityagenttool`，不再直接依赖 UnityEvalTool。将
`Runtime/Core/Prefabs/DebugPanel.prefab` 放入场景；该 Prefab 组合：

- UnityAgentTool 的 `DebugPanel` 与 `UnityAgentPanelModule`；
- `UnityDebugTool.SystemInfo` 的 `SystemInfoModule`；
- `UnityDebugTool.Performance` 的 `PerformanceMonitorModule`。

F8 显示或隐藏统一 Unity Agent 工作台；F10 同时显示或隐藏独立的 System Info 与 Performance。
两组显隐状态以及 Agent 浮窗的折叠状态彼此独立。

## System Info 集成

System Info 继续保留原有独立浮层逻辑。两个模块同时通过 `UnityAgentWorkspaceRegistry` 向工作台注册区块：

1. 上方先显示 System Info 行；
2. 下方显示 Performance 的 FPS、RAM 与 Audio 指标。

以下受保护资源继续保持原有视觉，并由独立界面与嵌入界面共同使用：

- `Runtime/SystemInfo/UI/SystemInfo.uxml` 与 `SystemInfo.uss`
- `Runtime/Performance/UI/PerformanceMonitor.uxml` 与 `PerformanceMonitor.uss`

项目自定义信息仍通过 `SystemInfoRegistry.Register(key, Func<string>)` 与 `Unregister(key)` 管理。

## 程序集

- `UnityDebugTool.SystemInfo`：Registry、Snapshot、View 与 Module，引用 `UnityAgentTool`。
- `UnityDebugTool.Performance`：Sampler、Snapshot、Graph View 与 Module，引用 `UnityAgentTool`。
- `UnityDebugTool.Editor`：在非 PlayMode 下向 Agent 工作台组合两个受保护视图。

本包不再包含 Debug Core、DebugWindow、Runtime Console 程序集或兼容层。
