# 变更记录

[English](CHANGELOG.md) | **简体中文**

## 未发布

- 将公共文档重构为一一对应的英文与简体中文指南；让仓库 README 成为完整的安装与
  首次使用入口；将可重现的源码打包与维护者自行定义的分发拆开；移除与宿主项目
  绑定的开发说明。
- 在产物预检中保留 npm metadata 查询失败，并使构建检查通过显式本地路径传递 tarball。
- 锁定 .NET SDK 10.0.300，从 SourceGenerator 程序集中排除源码控制 revision，并重新生成
  已提交 Analyzer，使字节级验证不受仓库布局影响。
- 修正构建自动化中已提交版本的求值方式，再执行 Package 校验。
- 准备版本 `2.0.2`：UnityEvalTool、Broker 与 npm Package 使用 2.0.2；
  UnityDebugTool 1.0.1 依赖 UnityEvalTool 2.0.2。
- 让多 Package 产物校验具备 SHA 绑定、并发安全、冒烟测试、版本预检，以及遇到已有不可变
  产物时的可恢复性。
- 将已提交 Unity Analyzer 存为普通 Git blob，确保 UPM Git 安装和二进制校验获得真实 DLL。
- 取消对 install/uninstall lifecycle 的依赖：全局 npm 安装/卸载前后通过明确、可检查的
  `unity service install|uninstall` 设置或移除服务。
- 在仓库与两个 UPM Package 许可证中一致保留继承的版权声明和当前版权。
- Unity eval 输出改为带正确顶层 error bit 的原生 MCP text/image block，不再把
  CallToolResult 形状的 JSON 嵌套为结构化文本。
- 每条 Unity 连接串行执行命令：排队期取消或超时的请求不再发送；已发送但被中断的命令
  保持明确的结果不确定状态，在解决前阻止后续执行。
- 让冷启动 auth token 发布跨进程原子化，限制未认证首帧和连接数，并为每条 WebSocket 关闭
  路径设置边界，超时后中止无响应 peer。
- 注册前校验完整 JavaScript Tool 树、可调用子 Tool resolver、显式 safety flag 和非保留导出名；
  增加持久数据风险 metadata 与感知 owner 的根移除。
- 在生成阶段诊断不支持的嵌套、异步和 JavaScript 保留名 C# Eval 函数，并让 Roslyn 集成测试
  不依赖其输出目录。
- 重做 UnityDebugTool 注册回滚、输入焦点、有界日志、递归 Tool 目录、性能 buffer 和 IL2CPP
  保留。视觉布局 metadata 不再隐式创建 Eval Tool，调用方使用显式 Tool 树。
- 将受支持的非 WebGL Release Player 中经认证的任意 JavaScript eval 作为正式 Runtime 契约保留，
  不依赖可选 UnityDebugTool UI。
- 在 `Packages` 下增加 `com.yuzetoolkit.unitydebugtool`，让 Runtime Debug UI 与 UnityEvalTool
  共享一个源码仓库，同时保留各自 Package README。
- 编译失败时通过上一次成功的 Unity 程序集保持 MCP/CLI 的 `CompilationFailed` repair mode
  可执行，同时继续拒绝编译/导入/重载过渡。
- 说清事件驱动编译等待与同进程跨 registry 变化的 handle 复用，并增加 Broker 状态策略
  回归测试。

## 2.0.1 - 2026-08-12

- CLI 控制台关闭或 Broker 租约过期时释放 Unity 侧 PuerTS session，包括在 Unity 临时断线期间
  延后释放。
- 隔离 Unity Broker Client 的连接 generation，防止已停止的重连循环拆除新建连接。
- 检测 Broker 进程替换，并重置属于上一 Broker 的 Unity 侧 session。
- 使用普通 Editor update 驱动的节流状态心跳，替换会自我维持的 Editor player-loop wakeup。
- 使 Broker、Unity Package、npm 和 Runtime 版本与已提交 `version.json` 版本保持一致。
- 将 Roslyn Generator 作为普通仓库源码保存在 `Roslyn`，不再向 Unity Package 嵌入源码归档。

## 2.0.0 - 2026-08-12

- 用绑定 `127.0.0.1:2347` 的电脑级 C# NativeAOT Broker 替换 Unity 内托管的 MCP 与 CLI listener。
- 为多个 Unity Editor 和 Player 进程增加认证注册与状态报告。
- 增加明确的编译、程序集重载、导入、Play Mode 过渡、断线和主线程卡死状态。
- 将 MCP 表面缩减为 `unity_status`、`unity_connect` 和 `eval`，并要求执行前必须完成发现和选择。
- 增加跨 Unity Domain Reload 继续运行的事件驱动就绪与编译完成等待。
- 增加原生 `unity` CLI，包含按项目路径自动选择、实例选择、单次命令和复用 Unity 现有解析器的
  交互控制台。
- 增加 macOS LaunchAgent、Linux systemd user unit 和 Windows 计划任务的当前用户服务集成。
- 增加面向 macOS、Linux 和 Windows x64/arm64 的 npm 打包及六平台产物构建矩阵。
- 将 Unity Package Manager Package 移到 `Packages/com.yuzetoolkit.unityevaltool`，将 Broker 源码移到 `Broker`。

该版本对协议和分发方式都有破坏性变更。请移除旧 UnityCLI 安装，并把 MCP Client 配置为经认证的
2347 端口。
