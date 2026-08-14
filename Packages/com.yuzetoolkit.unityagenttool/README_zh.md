# UnityAgentTool

[English](README.md) | **简体中文**

UnityAgentTool 是 Unity 2022.3 下 Editor 与 Runtime 共用的统一工作台。它依赖
`com.yuzetoolkit.unityevaltool`，并统一拥有 UI、运行时浮窗宿主、DebugPanel 生命周期、
DebugWindow Builder、Agent 对话、Command Line 会话和 Unity 日志查看器。

## 工作台

Editor 菜单 **YuzeToolkit > Unity Agent** 与运行时 `UnityAgentPanelModule` 都创建同一个
`UnityAgentWorkbenchView`。主侧栏固定包含五个主要操作：

1. **New conversation**：打开未落盘的新对话草稿；首次发送时才创建对话文档。
2. **New command line**：打开未落盘的命令行草稿；首次执行时才创建记录和当前进程的 VM。
3. **Debug Panel**：每个运行时 `DebugWindowModule.RegisterWindow(...)` 注册对应一个页签。Edit Mode
   可以正常打开空外壳，但只有进入 Play Mode 后才实例化依赖运行时数据的页面。
4. **Log**：从 Editor 域初始化或 Runtime 启动开始持续捕获，不依赖是否打开过 Log 页面；提供搜索、
   日志类型过滤、同类合并、清空、自动滚动、Stack Trace 级别、Editor 源文件跳转、本地日志文件入口、
   可滚动详情区以及可拖动的列表/详情分隔条。列表中的长日志始终限制为一行摘要且不会撑宽容器；选中后将
   完整消息放入突出卡片，并把每一条 Stack Frame 分别渲染为可读、可跳转源码的独立行。
5. **System Info**：在工作台内以 Unity Agent 风格的响应式卡片展示性能与系统信息；独立 Runtime 浮层仍保留原有样式。

Agent 与 Command Line 会话在侧栏中分组显示，各自保存独立输入草稿，并支持 Pin、归档与删除。
归档项不会出现在主界面，只能在 Settings 的两个独立归档页面恢复或永久删除。Settings 是独立的
全工作区页面，固定包含模型提供、组合配置、Eval 连接、Eval Tools 和两个归档管理页面。模型发现告警只在
Provider 页面内联显示，不再反复弹窗；所有自有下拉菜单都会限制在工作区视口内，供应商、Profile 或模型列表
较长时可纵向滚动。

Conversation 会渲染 User/Assistant 文本、待处理审批卡片，并把每个 Tool 调用显示为默认折叠的记录行。
展开后可以查看调用参数以及等待中、成功或失败的结果。Tool 消息仍会完整持久化并返回模型。
工作台继承当前 Unity PanelSettings / Theme 的字体，不打包、枚举、动态创建或
显式指定字体。

## Agent 循环

内建 HTTP Agent 使用刻意保持简单的顺序循环：先持久化一次模型响应，再为每个 ToolCall 按顺序写入且仅写入
一个 ToolResult，最后继续请求模型，直到模型不再调用工具。单轮模型步骤可配置，默认上限为 64。用户停止、
意外失败或 Unity Domain Reload 都会先为未完成工具补充明确的错误结果，再保存终态，后续轮次不会收到孤立的
工具协议。

Provider Profile 保存模型 Context Window。HTTP 对话接近窗口时，对话文档继续保留完整消息；发给模型的内容
改用一份语义摘要检查点和最近的完整消息边界。网络瞬时错误、429 与可恢复的 5xx 最多重试两次，并且只能发生
在收到第一条 SSE 事件之前；任何部分模型输出都不会重试。

内建 Editor/Runtime System Prompt 统一使用英文，声明 Unity 角色、文件、进程、Shell、Skill 与
`unity_eval_js` 的真实 Tool 名和首次选用路径，并要求陌生 Unity 工作先从 `tools://` 发现模块。每次模型请求
仍会携带完整结构化 Tool schema，具体参数与详细执行契约只由对应 Tool 描述维护。

## 独立 Agent 边界

AgentLoop、会话、审批、上下文压缩、Tool 调度和 `unity_eval_js` 全部在 Unity 进程内运行。默认 Host 直接创建
HTTP 模型 Provider 与 UnityEvalTool 的进程内 `EvalExecutor`，不会启动或连接 Codex、Broker、MCP 或电脑级 CLI。
Settings 中独立的 Eval 连接页面只管理外部程序访问 UnityEvalTool 的可选能力，不是 Agent 运行依赖。
Process/Shell Tool 也只有在模型明确调用时才启动指定程序。

OpenAI 模型通过 API Key 调用 OpenAI Responses API。ChatGPT/Codex 订阅不是可嵌入的 Provider 凭据，因此
UnityAgentTool 不读取 Codex 登录缓存，也不再提供 Codex App Server。历史 `codex-app-server` Profile 会迁移为
标准 OpenAI API 预设，之后需要 `OPENAI_API_KEY` 或本机保存的 API Key。

Editor 中若活动对话触发脚本编译，本包会先在 `Application.persistentDataPath` 写入同时绑定当前项目与 Editor
进程的恢复 marker，再中断并持久化该轮。成功编译与 Domain Reload 后，或失败编译结束后，系统会追加一次包含
编译错误/警告数量的续跑消息，并要求 Agent 重新检查 Unity 状态；Domain Reload 不会保留缓存 Unity 对象和
JavaScript VM。其它 Editor 进程留下的 marker 会被删除，不会在下次启动时自动执行旧任务。

## 持久化

所有机器级非密钥设置固定存放在 `Application.persistentDataPath/settings.json`：

```text
AgentConversations/       Agent 对话文档
CommandLineHistory/       命令行文档与当前选择状态
UnityAgentEditorCompilationRecovery.json  仅 Editor 使用的活动轮次恢复 marker
```

命令行输入、输出和草稿会跨 Unity 重启保存；JavaScript `EvalSession` 不恢复。Provider 密钥只写入
本机 `secrets.json`，不会进入项目默认配置。`Assets/Resources/UnityAgentProjectSettings.json`
保存可打入 Player 的无 Provider 默认值。只有当前 Editor 或 Player 不存在本机 `settings.json` 时，才从
这套默认值生成完整本机配置；已经存在或内容损坏的本机配置都不会被 Project Settings 覆盖。通过
**Edit > Project Settings > YuzeToolkit > Unity Agent** 编辑权限、Editor/Runtime Prompt、Tool 限制与
有序 AGENTS.md/Skill 根目录。Editor Play Mode 使用 Editor Prompt，Runtime Prompt 只用于独立 Player。

## Runtime 宿主

`DebugPanel` 管理唯一的全屏 `UIDocument`、`IDebugPanelModule` 生命周期与快捷键，全部实现已归入本包。
`UnityAgentPanelModule` 是 F8 统一工作台：标题栏拖动整个窗口，右上角手柄可以在面板边界内任意调整
宽高。折叠会真正隐藏全部内容与缩放命中区、释放焦点，并且不影响 System Info 的独立显隐。
窗口以左下角为锚点，几何通过 `PlayerPrefs` 保存。本包同时提供标准组合 Prefab，以及保留原视觉的
System Info / Performance。依赖方向为：

```text
UnityAgentTool -> UnityEvalTool
```

独立 UnityDebugTool Package 已删除。

## DebugWindow API

DebugWindow 注册已移动到本包，但继续使用 `YuzeToolkit` 命名空间：

```csharp
var handle = DebugWindowModule.RegisterWindow(window =>
{
    window.SetTitle("Player");
    window.AddReadOnly("State", () => player.StateName);
    window.AddPrimaryButton("Reset", player.Reset);
});
```

注册不依赖场景宿主。`DebugWindowModule` 只注册视觉窗口，不创建、注册或释放 `IEvalTool`；自动化入口必须
由功能所有者单独实现，并通过 `EvalToolRegistry.RegisterRootScoped` 独立注册和释放。`AddButton` 是普通动作，
`AddPrimaryButton` 用于页面主动作，`AddPreviousButton` / `AddNextButton` 用于方向操作。布尔、枚举、折叠、
范围和进度等默认控件均使用 Agent 调色板和包自有交互样式，不依赖 Unity 默认皮肤。

## 程序集

- `UnityAgentTool`：Agent Core、统一 UI、DebugPanel、DebugWindow、Command Line 与 Log。
- `UnityAgentTool.Editor`：EditorWindow 与 Editor Broker 设置桥接。

旧 Runtime Console registry、tab provider 程序集、Runtime Eval 页面、兼容 Provider 与
DebugWindow MonoBehaviour 宿主均已删除。
