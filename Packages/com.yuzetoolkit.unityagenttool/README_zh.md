# UnityAgentTool

[English](README.md) | **简体中文**

UnityAgentTool 是面向 Unity 2022.3 的进程内 Unity Agent 包。Agent 循环、工具、多对话、权限、模型适配和原生 JavaScript eval 都在 Unity 内运行，不经过 UnityEvalTool Broker、MCP 或 UnityEvalTool CLI。

## 安装

需要同时安装：

- `com.yuzetoolkit.unityevaltool` 2.0.2
- `com.yuzetoolkit.unitydebugtool` 1.0.1
- 本包 `com.yuzetoolkit.unityagenttool`

使用本地源码时，在 Package Manager 中选择 **Add package from disk**，再选择本包的 `package.json`。Agent 程序集直接引用 `UnityEvalTool`，不引用 `UnityEvalTool.Broker`、`UnityEvalTool.CLI` 或 Broker Editor 程序集。已经安装的 Broker 可以独立继续运行，但不是 Agent 的启动条件。

## 界面

Editor 只提供一个 `Unity Agent` 工作台。下面三个菜单项都会复用同一个 `EditorWindow`；`Open` 与 `Chat`
切换到对话页，`Settings` 切换到设置页：

- **YuzeToolkit > Unity Agent > Open**
- **YuzeToolkit > Unity Agent > Chat**
- **YuzeToolkit > Unity Agent > Settings**

本包通过 `RuntimeConsoleTabRegistry` 只向 Runtime Console 注册一个 **Unity Agent** 页签；Chat 与 Settings
都在同一工作台内部切换，不会再创建第二个 Console 页签或 `UIDocument`。Scene 中仍需放置并正确配置
UnityDebugTool 的 `Runtime/Core/Prefabs/DebugPanel.prefab`；本包不会自动创建 `DebugPanel`。

左侧栏使用紧凑的对话行。对话可以保持未分组，也可以拖入用户创建的分组并自由排序；同时支持置顶、归档，
以及在右键菜单中删除。左下角设置按钮只切换当前工作台页面。运行错误与危险确认使用居中的模态浮层，
不会再藏在页面底部。

工作台完整拥有自己的 UI Toolkit 外观。原生文本编辑仍负责 IME、选区和剪贴板语义，但按钮、字段、整数输入、
Toggle、Dropdown、模型菜单、右键菜单、Tooltip、滚动条和全部交互状态都由包内控件绘制，不使用 Unity 默认皮肤。

输入区只承载对话级选择：权限模式、Provider profile、模型和推理强度。有待发送文本时，唯一动作按钮执行发送；
如果当前 turn 仍在运行，则先停止并等待它结束，再把已捕获的文本发送到同一个对话；输入为空且当前 turn 正在运行时，
同一个按钮只执行停止。工作区和系统提示词不会出现在聊天页：所有对话都严格绑定
当前 Editor 项目根目录或构建后 Player 项目根目录，全局系统提示词只能在 Settings 中修改。

完整设置固定保存在 `Application.persistentDataPath/.unityagenttool/settings.json`，Unity 界面与外部编辑器修改的是
同一份文件；**Reload from disk** 会显式应用外部改动。对话历史根目录由设置中的单个稳定路径配置，默认是
`PersistentData + .unityagenttool`，每个对话文档实际位于其 `Sessions` 子目录。路径只保存稳定基点枚举与相对路径，
不会保存某台电脑的绝对路径。首次升级会从旧的 Editor `Library/UnityAgentTool` 或 Player
`persistentDataPath/UnityAgentTool` 非破坏复制设置与历史，源文件保留。Session API key 只保留在内存中；持久化
profile 可以记录环境变量名，但解析出的密钥不会写入设置或对话记录。

## Provider

所有 Provider 都转换到统一 Agent 调用契约，目前支持：

- OpenAI Responses API
- OpenAI-compatible Chat Completions API
- Anthropic Messages API
- Google Gemini Interactions API
- 通过本机 JSONL 进程通信的 Codex App Server

Chat 与 Settings 初次显示每个 profile 时会自动请求 Provider 的远端模型列表，也可以手工刷新。发现不可用时仍保留可编辑模型字段，并回退到内置的厂商目录，
快速填写默认 endpoint、模型限制和受支持的推理档位。目录覆盖 OpenAI、Anthropic、Google、xAI、Meta、
Kimi/Moonshot、GLM/Z.AI、Qwen、MiniMax、MiMo 与 DeepSeek，并为 Qwen 国际站和中国站分别提供 endpoint。
厂商模型会持续变化，因此远端发现结果始终优先于内置目录。

HTTP 协议下，**Base URL** 表示 HTTP API 根地址。选择 `codex-app-server` 后，同一存储字段会显示为 **Codex executable**，填写可执行文件名或绝对路径，默认是 `codex`。该模式启动 `codex app-server --stdio` 并使用本机现有 Codex 登录，因此 API key 字段会被禁用。

## Agent 工具与权限

内置工具包括文件/文件夹操作、本机进程与平台 shell、Skill 列表/读取，以及直接执行 Unity JavaScript。JavaScript 直接使用 UnityEvalTool 进程内 `EvalExecutor`，每个 Agent 对话拥有一个持久 eval session。

Agent 循环不再存在可配置或持久化的 `MaxSteps` 配额。它会持续进行模型与工具轮次，直到模型完成、用户停止、
Provider 失败或超时、工具报告真实错误，或者运行时边界终止当前 turn。上下文压缩会在完整 assistant/tool 边界上把
旧消息物理替换为有界摘要，并限制当前尾部内容；它用于控制请求和历史大小，不是工具调用步数限制。

权限模式：

- `FullAccess`：注册工具无需 UI 确认即可执行。
- `ConfirmWrites`：每个非只读工具都会暂停，并在聊天中显示 Approve/Decline 卡片。

直接执行 Unity JavaScript 和本机进程都属于高权限能力。只有在信任模型接口和指令来源时才应使用 `FullAccess`。

## AGENTS.md、Skills 与 Player 构建

AGENTS.md 与 Skills 使用两份相互独立、可排序的路径列表。每项都由稳定基点（ProjectRoot、PersistentData、UserProfile、Documents、Local/Roaming ApplicationData、TemporaryCache 或 StreamingAssets）、可选相对路径和 **Include in Player build** 开关组成；相对路径不能是绝对路径。默认值是显式、可删除且默认参与构建的 `ProjectRoot / .` AGENTS 项和 `ProjectRoot / .agents/skills` Skills 项。新增外部路径默认只供 Editor 使用，避免在不知情时将用户目录内容随产品发布。

Editor 按列表优先级读取全部路径，不受构建开关影响。Player 不读取主机实时设置路径，而只读取构建时已启用项按两份列表顺序生成的独立清单；删除默认 ProjectRoot 项或关闭它的构建开关后，对应项目内容不会再被隐式打包。

构建处理器只把配置的指令内容复制到生成的 `Temp` 暂存目录，并通过 Unity 2022 的 `BuildPlayerContext.AddAdditionalPathToStreamingAssets` 加入构建。它不会把外部 Skill 写入 `Assets` 或导入 AssetDatabase；符号链接会被拒绝，Unity `.meta` 文件会被跳过，并且存在单文件、总大小和文件数量限制。暂存目录会在构建结束后以及下一次构建前清理。

添加构建根目录前，应人工检查其中是否包含密钥、私人文档、过大资源或不应随产品发布的指令。

## 平台说明

- 本机进程与 shell 工具只适用于支持 `System.Diagnostics.Process` 的桌面型平台。
- WebGL 无法启动本机进程，浏览器网络请求还要求 Provider endpoint 正确开放 CORS。
- 移动端和主机平台通常不应暴露 shell。
- 桌面 Player 的 `StreamingAssets` 是普通文件系统路径。Android 与 WebGL 需要平台感知的打包内容读取实现，才能认为内置 AGENTS/Skill 加载已受支持。
- 目标平台是否能直接执行 Unity JavaScript，仍取决于可用的 PuerTS backend。

## Runtime Console 全局注册表

其它包可以不修改 DebugPanel prefab，直接注册进程级 Runtime Console 页签：

```csharp
private static IDisposable? registration;

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void RegisterTabs()
{
    registration = RuntimeConsoleTabRegistry.Register(
        "com.example.my-tabs",
        context => new IRuntimeConsoleTab[] { new MyRuntimeConsoleTab(context) });
}
```

注册必须发生在 `RuntimeConsoleModule` 初始化前。Factory id 必须唯一；Console Host 重新初始化时会重新调用 factory；释放返回的句柄后，后续 Host 初始化不再包含该 factory。
