# UnityEvalTool

[English](README.md) · [UnityEvalTool 包说明](Packages/com.yuzetoolkit.unityevaltool/README_zh.md) · [UnityDebugTool 包说明](Packages/com.yuzetoolkit.unitydebugtool/README_zh.md) · [Broker 协议](Packages/com.yuzetoolkit.unityevaltool/docs/BROKER_PROTOCOL.md)

UnityEvalTool 让电脑上的所有 Unity Editor 或 Player 主动连接同一个电脑级 Broker。AI Agent 和终端不再依赖 Unity 脚本域内部的监听器，因此即使 Unity 正在编译、程序集重载、进程退出或主线程卡住，外部仍然能看到准确状态并等待恢复；编译失败时 MCP/CLI 会通过上一次成功加载的程序集继续提供 repair mode。

仓库同时提供 UnityDebugTool：一个基于 UI Toolkit 的运行时调试面板与控制台，让玩家、开发者和 AI Agent 共享同一套 Tool 模型。

## 仓库结构

```text
UnityEvalTool
├── Packages/
│   ├── com.yuzetoolkit.unityevaltool/   # Broker 客户端、MCP eval 与 CLI runtime
│   └── com.yuzetoolkit.unitydebugtool/  # 运行时调试 UI 与控制台
├── Broker/
│   ├── src/                             # C# NativeAOT Broker 与 CLI
│   └── npm/                             # npm 入口包与原生平台包
├── Roslyn/                              # Source Generator 解决方案与测试
└── .github/workflows/
    └── release.yml                      # 六平台构建与发布
```

每个 Package 的安装细节和 API 由自己的 README 说明；本文档负责完整仓库、Broker 与发布流程。

## 安装

### 1. 安装 Unity Packages

#### UnityEvalTool

UnityEvalTool 需要 `com.tencent.puerts.core` 3.0.2 与且仅需一个 PuerTS JavaScript backend。本仓库已验证 `com.tencent.puerts.quickjs` 3.0.2；也可使用同一 PuerTS 发布系列中一组受支持的 V8 backend/core。不要同时安装多个 backend。然后在 Unity Package Manager 中选择 **Add package from git URL**，填入：

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.2
```

也可以修改 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.yuzetoolkit.unityevaltool": "https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.2"
  }
}
```

如果本仓库源码放在 Unity 项目的 `Game/UnityEvalTool`，开发时直接引用工作树，
无需复制 Package：

```json
"com.yuzetoolkit.unityevaltool": "file:../Game/UnityEvalTool/Packages/com.yuzetoolkit.unityevaltool"
```

#### UnityDebugTool

仓库 tag `v2.0.2` 内含 UnityDebugTool package `1.0.1`，它依赖 UnityEvalTool `2.0.2`。
先安装 UnityEvalTool，再添加可选的运行时调试 UI 包：

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unitydebugtool#v2.0.2
```

仓库嵌入项目开发时，直接引用两个工作树 Package：

```json
"com.yuzetoolkit.unitydebugtool": "file:../Game/UnityEvalTool/Packages/com.yuzetoolkit.unitydebugtool",
"com.yuzetoolkit.unityevaltool": "file:../Game/UnityEvalTool/Packages/com.yuzetoolkit.unityevaltool"
```

UnityDebugTool 的 prefab 配置、模块和 API 见它自己的 [Package README](Packages/com.yuzetoolkit.unitydebugtool/README_zh.md)。

### 2. 安装 Broker 与 CLI

```bash
npm install --global @yuzetoolkit/unityevaltool
unity service install
unity doctor
```

npm package 会安装 macOS、Linux、Windows x64/arm64 的原生 `unity` 可执行文件。
现代 npm 可能阻止 dependency lifecycle script，因此服务安装是明确的第二步；
`unity service install` 只创建当前用户服务，不创建系统级特权服务。确认它成功后再运行
`unity doctor`，不要关闭 npm 的 install-script 安全策略。

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

被中断的修改型 `eval` 不可自动重试；协议会明确告诉调用方是否可能已经执行。完整状态、等待语义和错误码见 [Broker 协议](Packages/com.yuzetoolkit.unityevaltool/docs/BROKER_PROTOCOL.md)。

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

卸载时必须趁 `unity` 可执行文件仍然存在先移除当前用户服务，确认第一条命令成功后再卸载 npm package。
npm 不会执行 uninstall lifecycle script：

```bash
unity service uninstall
npm uninstall --global @yuzetoolkit/unityevaltool
```

## 开发与发布

```bash
dotnet build Broker/UnityEvalTool.Broker.slnx -c Release
dotnet test Roslyn/UnityEvalToolRoslyn.sln -c Release
cd Broker
node --input-type=module -e "import { resolveAndValidateVersion } from './npm/scripts/version.mjs'; console.log(resolveAndValidateVersion(process.cwd()));"
node npm/scripts/pack-platform.mjs
node npm/scripts/pack-root.mjs
```

Broker、Roslyn 与两个 Unity Package 都以普通源码目录存放在该仓库中，不再依赖源码压缩包，
Package 开发也不会临时下载辅助源码。`version.json` 是发布版本的唯一来源；CI 会先验证
Broker、Unity Package、运行时常量和 npm metadata 版本一致，再构建六个 NativeAOT
平台包和一个平台无关入口包。发布必须显式开启并提供 npm 凭据，不会在普通构建中自动发生。
更完整的打包与发布检查见 [Broker/README.md](Broker/README.md)。

当这棵源码以 `Game/UnityEvalTool` 嵌入 RelicLight 时，一般 RelicLight 开发者只需把父仓库推送到
CNB，不需要配置本仓库的 GitHub remote、GitHub 写权限或第二份 checkout。只有同时维护两个仓库的
电脑才显式启用父仓库镜像 hook；hook 会对账完整源码树，并且只在 CNB 已接受准确的 RelicLight source
commit 后更新 GitHub。若后续 GitHub 更新失败，系统会保留可恢复的待发布状态，不会假装已经成功的
CNB 更新被回滚。

## 许可证

[MIT](LICENSE)
