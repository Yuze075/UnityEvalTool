# UnityEvalTool 2

[English](README.md) · [Unity 包说明](Packages/com.yuzetoolkit.unityevaltool/README_zh.md) · [Broker 协议](Packages/com.yuzetoolkit.unityevaltool/docs/BROKER_PROTOCOL_2.md)

UnityEvalTool 让电脑上的所有 Unity Editor 或 Player 主动连接同一个电脑级 Broker。AI Agent 和终端不再依赖 Unity 脚本域内部的监听器，因此即使 Unity 正在编译、程序集重载、进程退出或主线程卡住，外部仍然能看到准确状态并等待恢复。

## 仓库结构

```text
UnityEvalTool
├── Packages/
│   └── com.yuzetoolkit.unityevaltool/   # Unity Package Manager 包
├── Broker/
│   ├── src/                             # C# NativeAOT Broker 与 CLI
│   └── npm/                             # npm 入口包与原生平台包
└── .github/workflows/
    └── release.yml                      # 六平台构建与发布
```

## 安装

### 1. 安装 Unity 包

UnityEvalTool 需要 `com.tencent.puerts.core` 3.0.0 和一个 PuerTS JavaScript backend。在 Unity Package Manager 中选择 **Add package from git URL**，填入：

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.0
```

也可以修改 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.yuzetoolkit.unityevaltool": "https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.0"
  }
}
```

### 2. 安装 Broker 与 CLI

```bash
npm install --global @yuzetoolkit/unityevaltool
unity doctor
```

npm 包会安装原生 `unity` 可执行文件和当前用户后台服务，不会创建需要管理员权限的系统级服务。支持 macOS、Linux、Windows 的 x64 与 arm64。

## CLI 快速使用

```bash
unity list
unity                         # 按当前目录自动选择 Unity，进入控制台
unity connect <instance-id>   # 选择一个 Unity，进入控制台
unity Runtime getState        # 执行一次现有 Unity CLI 命令
unity eval-js --code "return 1 + 2;"
unity service status
```

交互控制台继续复用 Unity 侧原有命令解析器。`:status`、`:wait`、`:switch`、`:help` 和 `:quit` 是 Broker 自己的控制命令。

## MCP 配置

Streamable HTTP 地址固定为 `http://127.0.0.1:2347/mcp`。第一次安装会生成 `~/.unityevaltool/auth.json`。把其中的 `token` 配置成 MCP 请求头：

```text
Authorization: Bearer <token>
```

MCP 只提供三个工具：

1. `unity_status`：发现全部 Unity，并等待其可执行或等待一次新的编译周期结束。
2. `unity_connect`：使用已知注册表版本精确选择 `instanceId`，返回当前工作流专用的不透明 handle。
3. `eval`：在选中的 Unity 中执行；没有先查询和连接，或 Unity 当前不可安全执行时会明确拒绝。

被中断的修改型 `eval` 不可自动重试；协议会明确告诉调用方是否可能已经执行。完整状态、等待语义和错误码见 [Broker 2.0 协议](Packages/com.yuzetoolkit.unityevaltool/docs/BROKER_PROTOCOL_2.md)。

## 服务管理

```bash
unity service install
unity service status
unity service start
unity service stop
unity service restart
unity service uninstall
```

macOS 使用 LaunchAgent，Linux 使用 systemd user unit，Windows 使用当前用户计划任务。Broker 只绑定 loopback 的 2347 端口；端口被占用时会明确失败，不会偷偷更换端口。

## 开发与发布

```bash
dotnet build Broker/UnityEvalTool.Broker.slnx -c Release
cd Broker
node npm/scripts/pack-platform.mjs
node npm/scripts/pack-root.mjs
```

`.github/workflows/release.yml` 构建六个 NativeAOT 平台包和一个平台无关入口包。发布必须显式开启并提供 npm 凭据，不会在普通构建中自动发生。更完整的打包与发布检查见 [Broker/README.md](Broker/README.md)。

## 许可证

[MIT](LICENSE)
