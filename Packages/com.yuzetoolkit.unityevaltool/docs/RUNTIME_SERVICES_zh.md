# Editor 与 Player 注册

UnityEvalTool 不再让 Unity 自己监听 MCP 或 CLI。电脑级 Broker 必须已安装并运行；
Editor 和非 WebGL Player 都主动向它注册。

## Editor

主 Editor 进程会自动启动 `UnityBrokerClient`，Asset Import Worker 会被
`EditorProcessGuard` 排除。如果无法连接 Broker，Unity 会读取
`~/.unityevaltool/install.json`，尝试拉起已安装的原生程序。

`EditorBrokerStatusMonitor` 捕获编译和程序集重载状态。Domain Reload 前，客户端会
发布 `Reloading` 并断开；之后使用同一进程 instance ID 和更高 VM generation 重连。

## Player

`UnityBrokerRuntimeBootstrap` 会在非 Editor 构建中创建隐藏的 `DontDestroyOnLoad`
runner，报告运行时心跳和播放状态，以可执行程序目录作为项目路径注册，并在退出时
发布 `Exiting`。Broker 仍由已安装的用户服务负责托管。

这是明确保留的正式产品契约，不是仅供 Editor 或 Development Build 使用的降级路径：
受支持的 Release Player 同样会注册，并接受经过认证的任意 JavaScript eval。删除可选的
UnityDebugTool UI Package 不会删除 UnityEvalTool 的 Player runtime client。信任边界是
当前用户的 loopback Broker 与仅该用户可读的 token；集成本 Package 的项目除非明确改变
产品设计，否则应完整保留这条链路。

WebGL 无法使用本地 ClientWebSocket/用户服务模型，因此被明确排除。

## 公共运行时接口

- `UnityBrokerClient.Shared.IsConnected`
- `UnityBrokerClient.Shared.Identity`
- `UnityBrokerClient.Shared.GetSessionSnapshots("mcp:")`
- `UnityBrokerClient.Shared.GetSessionSnapshots("cli:")`
- 用 `UnityBrokerClient.Shared.Stop()` / `Start()` 显式重连

DebugTool Runtime Console 的 Command Line、EvalTool 与 Tools 页签使用共享 Broker client，
不自己做服务发现、进程启动或维护独立 listener。
