# UnityEvalTool 架构

## 进程边界

```text
AI MCP clients ──HTTP /mcp──┐
                            ├── C# NativeAOT Broker :2347 ──WebSocket /unity── Unity A
原生 unity CLI ───WS /cli───┘                                      └────────── Unity B
```

Broker 拥有发现、状态快照、事件驱动等待、选择租约、认证、MCP 协议、CLI 控制台和
请求路由。Unity 拥有主线程真实状态、编译/重载观察、PuerTS VM、helper tool 注册和
CLI 命令解析。

## 源码归属

| 区域 | 源码 | 职责 |
|---|---|---|
| 原生 Broker | 仓库 `Broker/src/UnityEvalTool.Broker` | 2347、注册表、MCP、CLI、用户服务管理 |
| npm 发行 | 仓库 `Broker/npm` | 平台选择、生命周期脚本、RID 平台包 |
| Unity 传输 | `Runtime/Broker` | 认证注册、心跳、中转请求、session |
| Editor 生命周期 | `Editor/Broker` | 稳定进程身份、编译/重载状态、拉起 Broker |
| Eval 引擎 | `Runtime/Core` 与 `Runtime/Tools` | PuerTS 执行和生成的 helper module |
| CLI 解析 | `Runtime/CLI/EvalCliCommandService*` | 现有命令语法和打印结果 |

目录名末尾的 `~` 让 Unity 忽略 .NET/npm 源码。

## 注册与选择

每次 Unity 注册包含进程内稳定的 `instanceId`、进程生命周期、项目路径、连接 epoch、
VM generation 和完整状态。成员变化会增加 `registryRevision`。`unity_connect` 必须
提交上一次状态快照的准确 revision，并生成随机 256-bit handle。因此选择属于每个
MCP 工作流或 CLI 控制台，不是 Broker 全局状态。

## 编译生命周期

Editor 观察每一次 `CompilationPipeline`。脚本域消失前会报告 `Compiling`、编译器
计数、`CompilationFailed` 和 `Reloading`。Broker 保留断开的实例和选择租约；重载后
同一进程以更高 epoch/VM generation 重连，并在主线程更新后报告 `Ready`。等待发生在
Broker 内部，不依赖 eval。编译失败不会卸载旧脚本域，而是进入可执行的 repair mode。

## 执行保证

- eval 前必须完成状态发现、明确连接并携带有效 handle。
- Unity 必须已连接，并且 `canEval` 或处于 `CompilationFailed` repair mode，Broker 才会转发请求。
- repair mode 使用上一次成功加载的程序集，以便读取错误、修改源码并再次刷新；失败源码不会被视为已加载代码。
- 每个 handle/CLI 控制台在 Unity 侧拥有持久 PuerTS session。
- 重连会改变 VM generation；旧脚本域丢失的 session 不会被伪装成仍然存在。
- 断开后结果不确定的修改请求绝不自动重试。

## 安全

Kestrel 只监听 loopback，并拒绝非 loopback Host/Origin。随机 token 保存在
`~/.unityevaltool/auth.json`，用于认证 MCP、Unity 与 CLI；Unix 权限仅允许当前用户
读写。端口冲突和协议不匹配都会明确失败。
