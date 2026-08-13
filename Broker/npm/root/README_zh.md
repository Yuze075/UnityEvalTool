# @yuzetoolkit/unityevaltool

[English](README.md) | **简体中文** | [完整文档](https://github.com/Yuze075/UnityEvalTool/blob/main/README_zh.md)

[UnityEvalTool](https://github.com/Yuze075/UnityEvalTool) 的原生 C# Broker、Streamable HTTP
MCP Server 和 `unity` CLI。npm 入口包会为 macOS、Linux 或 Windows 的 x64/arm64
选择匹配的原生包。

每个需要使用 UnityEvalTool 的 Unity 项目还必须安装 Unity 侧 UPM Package。

## 安装

```bash
npm install --global @yuzetoolkit/unityevaltool
unity service install
unity doctor
```

`unity service install` 会创建并启动绑定 `127.0.0.1:2347` 的当前用户后台服务：
macOS 使用 LaunchAgent，Linux 使用 systemd user unit，Windows 使用计划任务。
它不会安装特权系统服务。由于 npm 可能禁用 dependency lifecycle script，服务设置必须
显式执行；请检查该命令的退出状态。

通过 Unity Package Manager 添加 Unity 侧 Package：

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.2
```

打开 Unity 项目并等待编译完成，然后执行 `unity list`。只有看到该 Editor 才完成
第一次端到端注册检查；`unity doctor` 单独只检查 Broker 健康状态。

## CLI 快速使用

```bash
unity list
unity                         # 按当前项目目录选择
unity connect <instance-id>   # 精确选择 Unity 实例
unity Runtime getState        # 执行一次 Unity 侧命令
unity eval-js --code "return 1 + 2;"
unity tools
```

`unity` 和 `unity connect` 会打开交互控制台。其 Broker 控制命令是 `:status`、
`:wait`、`:switch`、`:help` 和 `:quit`；其它输入会发送给 Unity 命令解析器。

## MCP

将 Streamable HTTP MCP Client 连接到：

```text
http://127.0.0.1:2347/mcp
```

Broker 首次启动时会创建 `~/.unityevaltool/auth.json`。读取其中的 `token` 并发送：

```text
Authorization: Bearer <token>
```

不要提交该 token。MCP 流程始终是
`unity_status` → `unity_connect` → `eval`；必须先发现并明确选择。详见
[使用指南](https://github.com/Yuze075/UnityEvalTool/blob/main/README_zh.md) 和
[Broker 协议](https://github.com/Yuze075/UnityEvalTool/blob/main/Packages/com.yuzetoolkit.unityevaltool/docs/BROKER_PROTOCOL_zh.md)。

## 服务管理

```bash
unity service status
unity service start
unity service stop
unity service restart
unity service uninstall
```

Broker 只接受 loopback 流量，并在端口 `2347` 被占用时明确失败。

## 卸载

npm 不会自动执行服务卸载 helper。必须趁 `unity` 可执行文件仍存在时先移除
当前用户服务，确认成功后再移除全局 Package：

```bash
unity service uninstall
npm uninstall --global @yuzetoolkit/unityevaltool
```

如果第一条命令失败，应先解决它报告的服务错误，再继续卸载。

## 许可证

[MIT](https://github.com/Yuze075/UnityEvalTool/blob/main/LICENSE)
