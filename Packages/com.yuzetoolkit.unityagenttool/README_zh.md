# UnityAgentTool

[English](README.md) | **简体中文**

UnityAgentTool 是 Unity 2022.3 下 Editor 与 Runtime 共用的统一工作台。它依赖
`com.yuzetoolkit.unityevaltool`，并统一拥有 UI、运行时浮窗宿主、DebugPanel 生命周期、
DebugWindow Builder、Agent 对话、Command Line 会话和 Unity 日志查看器。

## 工作台

Editor 菜单 **YuzeToolkit > Unity Agent** 与运行时 `UnityAgentPanelModule` 都创建同一个
`UnityAgentWorkbenchView`。主侧栏固定包含五个主要操作：

1. **New conversation**：创建 Agent 对话。
2. **New command line**：创建持久化命令行记录，并在首次执行时按需创建当前进程的 VM。
3. **Debug Panel**：每个 `DebugWindowModule.RegisterWindow(...)` 注册对应一个页签。
4. **Log**：提供搜索、日志类型过滤、同类合并、清空、自动滚动、Stack Trace 级别、
   Editor 源文件跳转和本地日志文件入口。
5. **System Info**：显示下游包贡献的系统信息区块。

Agent 与 Command Line 会话在侧栏中分组显示。Settings 是独立的全工作区页面；Providers、
Agent defaults、Instructions、History 与 Eval Tool 都是真实页面，而不是滚动锚点。宽度低于
1024 px 时，设置导航收窄为 56 px 图标栏。

## 持久化

设置继续存放在 `Application.persistentDataPath/.unityagenttool/settings.json`。配置的历史根目录包含：

```text
Sessions/                 Agent 对话文档
CommandLineSessions/      命令行文档与当前选择状态
```

命令行输入与输出会跨 Unity 重启保存；JavaScript `EvalSession` 不恢复。选择一个历史会话后，
只会在当前 Unity 进程中按需创建全新的 VM。

## Runtime 宿主

`DebugPanel` 管理唯一的全屏 `UIDocument`、`IDebugPanelModule` 生命周期与快捷键。
`UnityAgentPanelModule` 是 F8 统一工作台：标题栏拖动整个窗口，右上角手柄可以在面板边界内任意调整
宽高。折叠会真正隐藏全部内容与缩放命中区、释放焦点，并且不影响 System Info 的独立显隐。
窗口几何通过 `PlayerPrefs` 保存。

UnityDebugTool 提供标准组合 Prefab，以及保留原视觉的 System Info / Performance。依赖方向为：

```text
UnityDebugTool -> UnityAgentTool -> UnityEvalTool
```

Agent 不反向引用 Debug。

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
