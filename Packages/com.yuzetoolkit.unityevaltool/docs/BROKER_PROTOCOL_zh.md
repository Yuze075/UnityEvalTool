# UnityEvalTool Broker 协议

[English](BROKER_PROTOCOL.md) | **简体中文** | [Package README](../README_zh.md)

本文档定义电脑级 UnityEvalTool Broker 与 Unity Client 之间的稳定边界。Broker 负责发现、
状态、选择、等待和中转；Unity 负责 PuerTS eval session、Tool 注册和 CLI 命令解析。

## 端点

- `http://127.0.0.1:2347/mcp`：MCP Streamable HTTP 端点。
- `ws://127.0.0.1:2347/unity`：Unity Client 连接。
- `ws://127.0.0.1:2347/cli`：交互式 CLI 连接。
- `http://127.0.0.1:2347/health`：Broker 健康状态快照。

Broker 只绑定 loopback。如果 `2347` 不可用，必须明确失败，不得静默改用其它端口。
token 认证默认关闭。在 Broker 进程环境中设置 `UNITYEVALTOOL_REQUIRE_TOKEN=true` 后，
会为 MCP、Unity 与 CLI 一并开启；健康状态快照通过 `requireToken` 报告实际模式。

## 消息封装

Unity 和 CLI WebSocket 每条 WebSocket 消息交换一个 UTF-8 JSON object：

```json
{
  "protocol": "2.0",
  "type": "request",
  "id": "globally-unique-request-id",
  "method": "eval/execute",
  "payload": {}
}
```

`type` 为 `request`、`response` 或 `event`。失败响应包含 `error` object，其中有稳定
`code`、人类可读的 `message`，以及在执行结果不确定时出现的 `mayHaveExecuted`。

## Unity 注册

Unity 的第一条消息必须是 `unity/register`。其 payload 包含：

- `authToken`：默认为空；仅在 Broker 已开启 token 认证时必填
- `instanceId`：在单个 Unity 进程的 Domain Reload 之间保持稳定
- `connectionEpoch`：每个 Unity 侧连接 generation 递增
- `processId` 和 `processStartedAtUtc`
- `projectName`、规范化 `projectPath`、`unityVersion`、`packageVersion`
- `environment`：`Editor` 或 `Player`
- 完整的初始 `status`

只有主 Unity Editor 进程可以注册。Asset Import Worker 绝对不得注册或启动 Broker。
成功响应包含 `brokerInstanceId`，它在当前 Broker 进程中唯一。如果该值改变，Unity
会丢弃所有保留的 PuerTS session，避免新 Broker 进程意外继承已不属于它的 session。

## 状态

Unity 发布 `unity/status` event。状态包含互相独立的传输和主线程观察，以及：

- `phase`：`Starting`、`Ready`、`Importing`、`Compiling`、`CompilationFailed`、
  `Reloading`、`PlayModeTransition`、`MainThreadStalled` 或 `Exiting`
- `canEval`
- `busyReason`
- `mainThreadTick`
- `isPlaying`、`isPaused`、`isUpdating`
- `compilationCycleId`、编译器错误/警告计数和上次编译时间戳
- `vmGeneration`

Broker 自行判定传输连接状态。存活 socket 不能证明 Unity 主线程仍在响应。如果主线程
tick 过期，即使 Unity 最后发布的 phase 是 `Compiling` 或 `Reloading`，Broker 也会派生
`MainThreadStalled`；`busyReason` 会保留最后一次报告的 phase。

## Broker 到 Unity 的请求

- `eval/execute`：`sessionId`、`requestId`、`code`、`timeoutSeconds`、`resetSession`
- `cli/execute`：`sessionId`、`requestId`、原始 `line`
- `session/release`：释放指定的 Unity 侧 eval session
- `broker/ping`：传输层存活检查

Unity 在 `canEval` 为 true 时执行 `eval/execute` 和 `cli/execute`。为兼容早于 repair-mode
`canEval` 标记的 Client，`CompilationFailed` 也是可执行的 repair mode；执行使用上一次
成功加载的程序集。Broker 绝不自动重试被中断的修改请求。

## 选择 handle

不存在进程全局的“已选 Unity”。`unity_connect` 会创建一个不透明、不可猜测的
`connectionHandle`，并将其绑定到一个已注册 `instanceId`。MCP 调用与 CLI 控制台各自
携带 handle。状态快照会返回 `registryRevision`；连接时必须提交该 revision，避免过期
发现结果在 registry 变化后静默指向错误目标。

在同一进程生命周期中，handle 可以跨 registry revision 变化和临时 Domain Reload 断线存活；
状态会显示新的 `connectionEpoch` 和 `vmGeneration`。revision 变化只影响新 handle 的创建。
现有 handle 在长时间无活动后过期，并在实例退出或被替换后失效。关闭 CLI 控制台、
替换其选择或租约过期时，都会释放对应的 Unity 侧 PuerTS session。如果 Unity 暂时断线，
Broker 会在同一进程生命周期内保留释放请求，并在重连后发送。

## 编译与重载

每一次被观察到的 Unity 编译都会获得 `compilationCycleId`，包括不是由 eval 发起的编译。
Unity 在 `CompilationPipeline.compilationStarted` 时发布 `Compiling`，在程序集编译完成时更新
编译器计数，出现错误时发布 `CompilationFailed`，并在程序集重载前发布 `Reloading`。
重连后，Unity 只在主线程稳定更新后才发布 `Ready`。

`unity_status` 可以等待 `ready` 或 `compilation-complete`。`ready` 表示可以执行，因此
正常 `Ready` 与 `CompilationFailed` repair mode 都会返回；`compilation-complete` 会在
编译成功或失败后返回。调用方必须检查 `phase`、`canEval` 和编译器计数。选择前按
`instanceId` 等待；选择后优先使用现有不透明 handle。等待在 Broker 中以事件驱动方式运行，
绝不在 Unity eval 内运行。使用 `compilationCycleId` 匹配 cycle；旧 `requestId` 状态参数
只是已弃用别名，绝不表示 `scheduleAssetRefresh` 返回的 Unity 侧 request ID。在可能触发编译
的 eval 之前，保留最新快照的 `capturedAtUtc`，并将其作为 `observedAfterUtc` 传入；
这会防止更早 cycle 或过期 `Ready` 样本提前完成等待。

## 稳定错误码

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
