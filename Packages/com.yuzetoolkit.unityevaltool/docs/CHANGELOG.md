# Changelog

此项目的所有显著更改都将记录在此文件中。

该格式基于[Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/),
并且参考[语义版本控制规范](https://semver.org/lang/zh-CN/)

本文件是属于Yuze的更新日志, 如有问题请联系邮箱[925581968@qq.com](mailto:925581968@qq.com)

## [Unreleased]

### Added

* 新增 UnityEvalTool Roslyn source generator，为带 `[EvalTool]` / `[EvalFunction]` 的 partial C# tool 生成 `IEvalTool` 元数据实现。
* `getCompilationState()` 现在返回最近一次刷新/编译请求的 id、类型、开始时间、pending 状态、跨 Domain Reload 的 Ready/Failed 状态以及编译错误/警告计数，方便 MCP/CLI 调用方确认真实结果。

### Changed

* 修复 Editor 脚本域重载时 MCP transport 取消回调重入关闭、旧 `HttpListener` 未及时释放固定端口的问题。
* Tool import 协议统一为 `tools://` / `tools://<Tool/Path>`，删除旧 `tool:`、裸路径 fallback、兼容 alias 和旧分层入口。
* `EvalScriptLoader` 收敛为两个判断：`tools://` 只走 Tool 加载流程，其他 import 只走当前 `ILoader`，未设置自定义 loader 时使用 PuerTS `DefaultLoader`。
* C# tool 只通过 `IEvalTool` 实例注册，删除 `EvalToolBase`、`EvalMutableTool`、反射式泛型注册和内置 root 封装层。
* JavaScript tool 改为初始化阶段按 module path 注册；根索引只读取缓存 metadata，具体 `tools://<path>` import 时才通过当前 loader 加载 module，删除运行时字符串、文件夹、reload 和 unload API。
* `EvalToolFunctionDescriptor` 删除重复的 `ParameterTypes`，函数 metadata 只保留有序 `Parameters`。
* 内置 C# tool 改为 partial class，并通过生成的 `IEvalTool` 元数据提供工具说明、函数说明、参数顺序、参数类型和默认值。
* C# tool 禁用后仍可 import 对应 `tools://<Tool/Path>` module 并读取 metadata / `isEnabled()`；实际调用时继续由 registry 拒绝 disabled tool。
* `EvalToolRegistry` 增强生成元数据校验，并改为管理根 `IEvalTool` 实例。
* PuerTS binding 配置补齐 Runtime/Player 侧 `ToolManagerTool`；Editor-only Tool 不参与 binding。
* MCP 和 CLI bridge 只保留 `eval` 方法名，不再保留旧兼容方法名。
* Runtime 文档统一为 `McpServer.Shared.Start(...)` / `StartWithOwner(...)` 启动方式。
* 补充loader-backed JS 绝对路径、CLI 注册文件、MCP/CLI token 默认关闭、MCP 非 0 固定端口等安全和生命周期说明。
* Editor CLI 与 MCP 自动恢复都在 `InitializeOnLoadMethod` 中直接执行；MCP 不再通过 `EditorApplication.delayCall` 二次调度，避免 Unity 窗口未聚焦时 listener 启动被延后。
* MCP、CLI 自动启动和编译监控统一拒绝 Asset Import Worker；MCP 固定端口只由主 Editor 监听，域切换中的瞬时端口占用通过有限 PlayerLoop 重试恢复，单次失败不再清除用户的 AutoStart 意图。
* 编译请求改由 `CompilationPipeline`、程序集编译结果和 `AssemblyReloadEvents` 推进状态，不再通过 `EditorApplication.update` 采样猜测编译开始与结束。
* `EvalValueFormatter` 在深度边界优先保留 Dictionary/List 结构，避免嵌套 metadata 退化成 `System.Collections...` 字符串。
* 文档中的 helper import 路径统一为 `tools://` / `tools://<Tool/Path>`，并补充新的 JS Tool authoring 说明。
* `scheduleAssetRefresh()`、`Assets.refreshNow()` 和脚本写入后的 refresh 改为脚本安全刷新流程：PlayMode 中先请求退出播放，稳定回到 EditMode 后再强制刷新导入并请求脚本编译。
* 修复 PlayMode 退出产生的 Domain Reload 会误将待处理刷新/编译请求标记为 Ready 的问题；请求现在只会发出一次停止播放指令，并且只有在稳定 EditMode 真正派发、观察到目标 CompilationPipeline 编译并进入对应程序集重载后才会完成。PlayMode 转换门禁改由 Editor update 清理，避免后台 Unity 的 delayCall 不推进而永久阻塞 eval。
* Tool metadata 删除集中 safety 推断；导出函数需要用 `[EvalFunction(..., Safety = ...)]` 显式声明安全语义，避免路径、方法名和说明文字硬编码漂移。
* Tool metadata 和 CLI help 为未手写 `[EvalParameter]` 的常见参数补充稳定说明。
* `Importers.setMany()` 和 `Serialized.setMany()` 同时支持 `{ propertyPath: value }` map 与 `{ propertyPath, value }[]`。
* `Inspect` tool 会先把 selector 快照解析为当前 GameObject，再格式化当前对象状态。

## [1.0.0] - 2026-5-1

### Added

* 初始版本更新和提交
* 新增基于 `[EvalTool]` C# class 的工具目录，项目或其他包可通过 C# 注册扩展工具。
* 新增 Server Window 工具管理区，可刷新 JavaScript 工具并持久化启用/禁用状态。

### Changed

* helper import 路径从旧的分层入口调整为 `tools/<name>`，不再需要 `.mjs` 后缀。
* 包内内置 helper 改为 C# 虚拟模块。
* C# tool 不再要求继承 `IEvalTool`；运行时注册改为 `[EvalTool(name, description)]` + `EvalToolRegistry.Register<TTool>() where TTool : class, new()`。
* `IEvalTool` 移除 Editor-only 属性和默认 `Functions` 实现，保留为后续代码生成契约。
* 内置 Runtime tools 移动到 `Tools/Runtime`，内置 Editor tools 移动到 `Tools/Editor`；两个 tool 程序集自行注册自己的工具和 PuerTS binding 配置。
* 主 Runtime 程序集只保留网络、会话、JS loader/catalog/formatter 等核心逻辑；主 Editor 程序集只保留菜单、Server Window 和启动配置。
* 新增 Editor-only JavaScript 测试 helper `tools/editorJsTest`，用于验证 JS helper 发现与执行流程。
* 包内 C# tool 的 public 返回类型改为显式类型，不再用 `object` 隐藏工具协议。
* `eval` 结果转换移动到 MCP 服务端出口：基础类型、List、Dictionary 统一作为 JSON text content 返回，UnityObject 和 C# 自定义对象由 `EvalValueFormatter` 摘要。
* `LitJson` 收敛为 JSON parse/stringify；字典/列表构造和参数读取移动到 `EvalData`。
