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

## 持久化

所有机器级非密钥设置固定存放在 `Application.persistentDataPath/settings.json`：

```text
AgentConversations/       Agent 对话文档
CommandLineHistory/       命令行文档与当前选择状态
```

命令行输入、输出和草稿会跨 Unity 重启保存；JavaScript `EvalSession` 不恢复。Provider 密钥只写入
本机 `secrets.json`，不会进入项目默认配置。`Assets/Resources/UnityAgentProjectSettings.json`
保存可打入 Player 的无 Provider 默认值；本机设置缺失或无法解析时回到这套默认值。

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
    window.AddButton("Reset", player.Reset);
});
```

注册不再依赖场景宿主。显式 `DebugEvalToolBuilder` 根 Tool 会立即进入 `EvalToolRegistry`，释放句柄时
同步移除。视觉控件使用 Agent 调色板和自有交互样式，不再复用旧 DebugWindows USS。

## 程序集

- `UnityAgentTool`：Agent Core、统一 UI、DebugPanel、DebugWindow、Command Line 与 Log。
- `UnityAgentTool.Editor`：EditorWindow 与 Editor Broker 设置桥接。

旧 Runtime Console registry、tab provider 程序集、Runtime Eval 页面、兼容 Provider 与
DebugWindow MonoBehaviour 宿主均已删除。
