# UnityEvalTool

UnityEvalTool 通过一个电脑级 Broker，让 AI Agent 和终端用户操作 Unity。Unity
不再自己监听 MCP 端口，也不再额外开启随机 CLI 端口。每个 Editor 或 Player
只向 Broker 建立一条经过认证的 WebSocket；MCP 与 CLI 共同通过它中转，并继续
使用现有的 PuerTS eval、helper tool 与命令解析系统。

[English](README.md) · [协议](docs/BROKER_PROTOCOL.md) · [Helper 模块](docs/HELPER_MODULES_zh.md)

## 组成

- Unity Package Manager 包 `com.yuzetoolkit.unityevaltool`：注册客户端、编译与重载
  状态监控、PuerTS eval session、helper tool，以及 Unity 侧 CLI 命令解析器。
- npm 包 `@yuzetoolkit/unityevaltool`：安装原生 `unity` 命令和绑定
  `127.0.0.1:2347` 的当前用户后台服务。
- 原生平台包：支持 macOS、Windows、Linux 的 x64 与 arm64。JavaScript 只负责
  npm 平台选择和拉起进程；Broker 与 CLI 都是 C# NativeAOT。

## 安装

UnityEvalTool 需要 `com.tencent.puerts.core` 与且仅需一个 PuerTS backend。本仓库已验证
`com.tencent.puerts.quickjs` 3.0.2 与其匹配的 core 3.0.2；也可使用同一 PuerTS 发布系列中一组受支持的 V8 backend/core。
先通过 Package Manager 添加 Unity 包，然后安装电脑级包：

```bash
npm install --global @yuzetoolkit/unityevaltool
unity service install
unity doctor
```

在 Unity Package Manager 中使用这个 Git URL：

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.2
```

Service 安装是明确步骤，因为现代 npm 可能阻止 dependency lifecycle script。
`unity service install` 会安装并启动当前用户的 LaunchAgent（macOS）、systemd user unit
（Linux）或计划任务（Windows），不需要管理员级系统服务。可使用
`unity service status|start|stop|restart|uninstall` 显式管理。

Broker 会生成仅当前用户可读的 `~/.unityevaltool/auth.json`。MCP 客户端连接
`http://127.0.0.1:2347/mcp`，并把其中 token 作为
`Authorization: Bearer <token>` 发送。Unity 与 CLI 使用同一份本机 token。

## MCP 固定流程

Broker 只暴露三个工具：

1. `unity_status`：列出全部 Unity 进程及其状态。在选择前可以按 `instanceId`
   等待 `ready` 或 `compilation-complete`；选择后也可以按 `connectionHandle` 等待。
   两种等待都可能以 `CompilationFailed` 结束，必须检查 `phase`、`canEval` 和编译计数。
2. `unity_connect`：使用上一步快照返回的 `registryRevision` 精确选择
   `instanceId`，返回仅属于当前工作流的不透明 handle。
3. `eval`：在已选择的 Unity 中执行现有的
   `async function execute() { ... }` 契约。它必须携带 handle，Unity 忙碌时会被拒绝。

Agent 必须先完成状态查询和连接，之后才能 eval。handle 不是全局选择；同一个
Unity 进程发生 registry 变化或 Domain Reload 后仍可继续使用，空闲会过期，Unity
进程被替换时会失效；registry 变化本身不需要创建新 handle。被中断的 eval 永远不会自动重试。

Unity 会独立报告 `Ready`、`Importing`、`Compiling`、`CompilationFailed`、
`Reloading`、`PlayModeTransition` 以及退出和连接状态。因此即使脚本域暂时不存在，
Agent 仍可以在 Broker 中等待。编译失败后 MCP/CLI 进入 repair mode，继续通过上一次
成功加载的程序集读取错误、修改代码并再次刷新。

## CLI

```bash
unity list
unity                         # 按当前项目路径自动选择，进入交互控制台
unity connect <instance-id>   # 明确选择后进入交互控制台
unity Runtime getState        # 自动选择并执行一次
unity connect <id> -- Editor getCompilationState
unity eval-js --code "return 1 + 2;"
```

交互控制台中的 `:status`、`:wait`、`:switch`、`:help`、`:quit` 由 Broker 解析；
其它行原样交给 Unity 的 `EvalCliCommandService`，因此 DebugTool 和现有 helper 命令
流程保持不变。

## 固定本机端点

| 端点 | 用途 |
|---|---|
| `http://127.0.0.1:2347/health` | Broker 健康状态 |
| `http://127.0.0.1:2347/mcp` | MCP Streamable HTTP |
| `ws://127.0.0.1:2347/unity` | Unity 注册与中转 |
| `ws://127.0.0.1:2347/cli` | 原生 CLI 控制台 |

Broker 只绑定 loopback；2347 被占用时会明确失败，不开放局域网模式，也不会静默
换端口。

非 WebGL 的 Release Player 会有意保留与 Editor 相同、经过认证的任意 JavaScript eval
能力；它不受 Development Build 开关控制，也不依赖可选的 UnityDebugTool UI Package。
完整边界见 [Editor 与 Player 注册](docs/RUNTIME_SERVICES_zh.md)。

## 开发与发行

NativeAOT 源码和 npm 流水线位于仓库根目录的 `Broker`。发行矩阵构建六个 RID
平台包和一个入口包。本机可在该目录执行当前平台打包验证；npm 发布是独立的显式操作。
