# UnityDebugTool

基于 UI Toolkit 的 Unity 运行时 Debug 面板，依赖 `UnityEvalTool`，用于同时服务人类调试和 AI 调试。

[仓库总览](../../README_zh.md) · [English](README.md)

## 安装

先安装 `com.yuzetoolkit.unityevaltool`，再通过 Unity Package Manager 的 **Add package from git URL** 添加本包：

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unitydebugtool#v2.0.2
```

仓库放在项目的 `Game/UnityEvalTool` 时，开发期间直接引用本地工作树：

```json
"com.yuzetoolkit.unitydebugtool": "file:../Game/UnityEvalTool/Packages/com.yuzetoolkit.unitydebugtool"
```

需要在 Scene 或常驻 prefab 中放置 `Runtime/Core/Prefabs/DebugPanel.prefab` 才会启用 UI；本包不会自动创建面板。

## 能力

- 场景放置式运行时 `DebugPanel`。
- 通过静态 builder API 注册左上角 Debug 窗口；没有 `DebugPanel` 实例时只缓存注册信息。
- `DebugPanel` 实例启用后可选注册动态 `IEvalTool`，让 AI 直接读写运行时字段或触发按钮。
- 内置性能 HUD：
  - FPS、帧耗时、平均 FPS、1% low 平均、0.1% low 平均。
- 内置系统信息 HUD：
  - 屏幕、窗口、Graphics API、GPU、VRAM、最大贴图尺寸、Shader Level、CPU、RAM、OS。
  - 通过 `SystemInfoRegistry.Register(key, Func<string>)` 在运行时注册自定义信息。
- 注册式 Runtime Console 页签：
  - `Log`：Unity Console 风格工具栏、过滤、Collapse、列表与详情区。
  - `Command Line`：当前进程内持久、逐行提交的 UnityEvalTool CLI session 与输出历史。
  - `EvalTool`：UnityEvalTool 启停、重连、Broker 注册、Unity 状态和可执行性。
  - `Tools`：递归展开的完整 Runtime Tool 目录，包含每条路径、函数、参数、安全声明和有效启用状态。
- Runtime Console 使用可手动缩放的 UI Toolkit 外壳。各页签不使用 UI Toolkit `ScrollView`；超出内容由包内轻量 PanView 处理，支持鼠标滚轮平移和紧凑自定义滚动条。

默认模块快捷键：`F10` 打开/关闭 Performance 与 System Info，`F9` 打开/关闭 Debug Windows，`F8` 打开/关闭 Runtime Console。`Ctrl` / `Alt` 修饰键仍由 `DebugPanel` 统一配置；多个模块配置为同一个按键时会一起打开、一起关闭。

`DebugPanel` 不会自动创建。需要在场景或常驻 prefab 中放置本包自带的 `Runtime/Core/Prefabs/DebugPanel.prefab`；同一进程只允许一个启用中的 `DebugPanel` / `DebugWindowModule` Host，重复 Host 会显式初始化失败，避免同一个 Tool 根出现所有权歧义。静态 `DebugWindowModule.RegisterWindow(...)` 可以先调用，但只有该 prefab 实例存在并启用后，UI 和 EvalTool 才会激活。同一托管运行时内，窗口注册可跨 `DebugPanel` Host 销毁保留，直到调用返回的 `IDisposable` 句柄 `Dispose()`；正常 Unity Domain Reload 会重置静态注册，持有者应在自身正常初始化流程中重新注册。

Debug Window 以指针交互为主。Button、Foldout、Slider、数值字段和窗口空白不会获得键盘或手柄焦点；数值字段仍可通过自身的指针拖动区域调整，不支持指针编辑的对象类值会显示为只读。只有鼠标左键明确点击某个可写字符串字段后，该字段才接收键盘输入；按 Enter、点击其它位置、切换页签或隐藏面板都会结束编辑并清理 EventSystem selection。Runtime Console 的搜索框和 Command Line 输入框遵循同一规则。此策略避免遗留 UI 焦点把后续 Submit/导航变成误操作；它无法阻止项目独立注册的 gameplay `InputAction` callback，需要模态文本输入的宿主项目仍应在编辑期间门禁自己的 gameplay action map。

## 运行时结构

面板根对象只保留最小职责：

- `DebugPanel` 只负责 `UIDocument` 初始化、根节点显隐、快捷键输入和模块生命周期。
- `IDebugPanelModule` 暴露自己的 `ToggleKey`，并只接收 `DebugPanelContext`，不再接收整个面板对象。模块通过 context 创建自己的 layer、加载自己的 USS。
- `DebugWindowModule` 只负责挂载 Debug 窗口，静态注册列表交给 `DebugWindowRegistry`。
- `PerformanceMonitorModule` 只协调 `PerformanceSampler` 和基于 UXML 的 `PerformanceMonitorView`。
- `SystemInfoModule` 只负责渲染 `SystemInfoRegistry` 中的信息项，并使用独立 UXML/USS。
- `RuntimeConsoleModule` 只承载挂在 prefab 上的 `IRuntimeConsoleTabProvider` 返回的页签。没有 provider 时不创建 RuntimeConsole 界面。

当前运行时代码结构：

```text
Runtime/
  Core/
    UnityDebugTool.asmdef
    DebugPanel、模块接口、context、UI Toolkit 模板辅助
    Prefabs/        DebugPanel prefab
    Settings/       PanelSettings asset 与 Unity 默认主题导入
  DebugWindows/
    UnityDebugTool.DebugWindows.asmdef
    builder API、节点模型、EvalTool 适配、窗口注册/模块
    UI/             Debug 窗口 USS
  Performance/
    UnityDebugTool.Performance.asmdef
    采样器、快照、HUD 视图、图表元素、模块
    UI/             Performance UXML 与 USS
  SystemInfo/
    UnityDebugTool.SystemInfo.asmdef
    注册表、快照、HUD 视图、模块
    UI/             System Info UXML 与 USS
  RuntimeConsole/
    Core/
      UnityDebugTool.RuntimeConsole.asmdef
      module、页签 provider 接口、页签视图、基础 USS
    Log/
      UnityDebugTool.RuntimeConsole.Log.asmdef
      Unity Console 风格日志页签
    CliRepl/
      UnityDebugTool.RuntimeConsole.CliRepl.asmdef
      UnityEvalTool 命令行页签
    EvalTool/
      UnityDebugTool.RuntimeConsole.EvalTool.asmdef
      UnityEvalTool 状态与控制页签
    Tools/
      UnityDebugTool.RuntimeConsole.Tools.asmdef
      Runtime Tool 目录与控制页签
Tests/Editor/       注册、安全声明、目录与有界日志契约测试
link.xml            IL2CPP 下反射调用 Debug Eval Tool 适配器的保留规则
```

程序集边界：

- `UnityDebugTool`：位于 `Runtime/Core` 的 Core assembly。依赖 `Unity.InputSystem` 与用于根级焦点清理的 Unity EventSystem API，不依赖 `UnityEvalTool`。
- `UnityDebugTool.DebugWindows`：Debug window builder/module 与 EvalTool 适配，依赖 `UnityEvalTool`、Input System 与 Unity EventSystem API。
- `UnityDebugTool.Performance`：右上角 FPS/RAM/Audio HUD 模块，不依赖 `UnityEvalTool`。
- `UnityDebugTool.SystemInfo`：右下角系统信息 HUD 模块与公开注册 API，不依赖 `UnityDebugTool.Performance` 或 `UnityEvalTool`。
- `UnityDebugTool.RuntimeConsole`：Runtime Console Core，依赖 `UnityDebugTool`、Input System 与用于焦点清理的 Unity EventSystem API。
- `UnityDebugTool.RuntimeConsole.Log`：Unity 日志页签，依赖 Runtime Console Core。
- `UnityDebugTool.RuntimeConsole.CliRepl`：Command Line 页签，依赖 `UnityEvalTool` 和 `UnityEvalTool.CLI`。
- `UnityDebugTool.RuntimeConsole.EvalTool`：UnityEvalTool 状态与连接控制页签，依赖 `UnityEvalTool.Broker`。
- `UnityDebugTool.RuntimeConsole.Tools`：完整 Tool 目录与独立开关页签，依赖 `UnityEvalTool`。

## UXML 与 USS

固定 UI 结构优先放进 UXML。Runtime Console 页签是注册式视图，行与服务卡片由运行时数据驱动，因此用 C# 创建。

当前模板：

- `Runtime/Performance/UI/PerformanceMonitor.uxml`：FPS/RAM/Audio HUD 结构。
- `Runtime/SystemInfo/UI/SystemInfo.uxml`：System Info HUD 结构。
- Runtime Console 不再使用单个 UXML 模板。Core 创建页签外壳，挂在 prefab 上的 provider 创建各自页签。
- Runtime Console 页签刻意不使用 UI Toolkit `ScrollView`。运行时数据内容溢出时使用 `RuntimeConsoleUi.CreatePanView()` 创建带自定义滚动条的滚轮平移内容区。

USS 由使用它的模块自己持有。Core 不再提供共享 root USS。`Runtime/Core/Settings/DebugPanelDefaultTheme.tss` 只导入 Unity 内置默认主题，保证 UI Toolkit 控件能正常绘制。

- `Runtime/DebugWindows/UI/DebugWindows.uss`：注册式 Debug 窗口 UI。
- `Runtime/Performance/UI/PerformanceMonitor.uss`：FPS/RAM/Audio HUD。
- `Runtime/SystemInfo/UI/SystemInfo.uss`：System Info HUD。
- `Runtime/RuntimeConsole/Core/UI/RuntimeConsoleCore.uss`：Runtime Console 页签外壳。
- `Runtime/RuntimeConsole/Log/UI/RuntimeLogTab.uss`：Unity Console 风格日志页签。
- `Runtime/RuntimeConsole/CliRepl/UI/RuntimeCliReplTab.uss`：Command Line 页签。
- `Runtime/RuntimeConsole/EvalTool/UI/RuntimeEvalToolTab.uss`：EvalTool 状态与控制页签。
- `Runtime/RuntimeConsole/Tools/UI/RuntimeToolsTab.uss`：Tools 目录页签。

内置模块和 Runtime Console 页签 provider 通过 prefab 序列化字段显式引用自己的 USS，不使用 `Resources` 运行时加载。`DebugPanel` 只清空并暴露 `UIDocument` 根节点，然后驱动模块生命周期。

内置模块或页签 provider 只有在自身 `MonoBehaviour` 启用时才会初始化；缺失必需 UXML/USS 会显式失败。任一模块初始化抛异常时，`DebugPanel` 会按逆序关闭已经启动的模块，并且不会逐帧反复重试。

## 推荐的显式视觉树与 Tool 树

```csharp
using System;
using YuzeToolkit;

public sealed class PlayerDebugHandle : IDisposable
{
    private int _hp = 10;
    private bool _invincible;
    private readonly IDisposable _registration;

    public PlayerDebugHandle()
    {
        var tool = new DebugEvalToolBuilder(
            "PlayerDebug",
            "Stable runtime player debug controls.");
        tool.AddWritable("Hp", "Read or set the player's HP.", () => _hp, value => _hp = value)
            .AddWritable("Invincible", "Read or set invincibility.", () => _invincible,
                value => _invincible = value)
            .AddButton("Kill", "Set player HP to zero.", () => _hp = 0,
                EvalToolSafety.Destructive | EvalToolSafety.RequiresConfirmation);

        _registration = DebugWindowModule.RegisterWindow(
            tool,
            window =>
            {
                window.SetTitle("Player");
                window.AddSegmentedInt("HP", 0, 10, () => _hp, value => _hp = value);
                window.AddBoolButton("Invincible", () => _invincible, value => _invincible = value);
                window.AddButton("Kill", () => _hp = 0);
            });
    }

    public void Dispose()
    {
        _registration.Dispose();
    }
}
```

视觉树只负责布局，显式 `DebugEvalToolBuilder` 负责稳定自动化路径。两棵树必须复用同一组 getter、setter 和 action，但 Foldout 或横向布局绝不能决定 Tool 身份。使用该重载时，视觉节点上的 `toolName` metadata 会被忽略。代表的运行时对象销毁时必须 Dispose 返回句柄；该操作会同时删除窗口及它准确持有的根 Tool。

旧 `RegisterWindow(toolName, description, configure)` 重载已经废弃。它现在只创建视觉窗口，传入名称仅作为视觉 metadata，绝不会注册 Eval Tool；所有自动化入口都应迁移到显式双树重载。视觉 builder 的 `toolName` / `description` 参数暂时保留源码兼容，但不会参与 Tool 注册。

没有填写工具名和说明的窗口只显示 UI，不注册 EvalTool：

```csharp
DebugWindowModule.RegisterWindow(window =>
{
    window.SetTitle("Local Debug");
    window.AddReadOnly("State", () => "Ready");
});
```

系统信息每个注册 key 固定显示为一行：

```csharp
SystemInfoRegistry.Register("Player State", () => player.StateName);
SystemInfoRegistry.Unregister("Player State");
```

## EvalTool 用法

上面的显式示例会稳定生成 `PlayerDebug/Hp`、`PlayerDebug/Invincible` 和 `PlayerDebug/Kill`，不受视觉分组变化影响。

```javascript
async function execute() {
  const hp = await import("tools://PlayerDebug/Hp");
  const before = hp.get();
  const after = hp.set(20);

  const kill = await import("tools://PlayerDebug/Kill");
  kill.invoke();

  return { before, after };
}
```

可以通过 `tools://` 和 `getToolDetails("PlayerDebug")` 查看完整树。

## Builder API

- `SetTitle(string title)`
- `SetDraggable(bool draggable)`
- `AddLabel(string text)`
- `AddSpace(float height = 8)`
- `AddReadOnly<T>(string label, Func<T> getter, string? toolName = null, string? description = null)`
- `AddValue<T>(string label, Func<T> getter, Action<T> setter, string? toolName = null, string? description = null)`
- `AddField<T>(string label, Func<T> getter)` / `AddField<T>(string label, Func<T> getter, Action<T> setter)`：兼容旧 debug-ui 的字段入口。
- `AddReadOnlyBool / AddReadOnlyInt / AddReadOnlyFloat / AddReadOnlyString(...)`
- `AddBool / AddInt / AddFloat / AddString(...)`
- `AddSlider(...)`：float / int 真滑条，带填充条和值标签；float / int 都支持旧 `"{0:F2}"` composite format 与新 `"0.##"` 数字格式。
- `AddProgress(...)` / `AddProgressBar(...)`：float / int 进度条。
- `AddButton(...)`
- `AddImage(...)`：静态或动态 `Texture2D`、`Sprite`、`RenderTexture`、`VectorImage` 预览。
- `AddGroup(string label, Action<DebugGroupBuilder> configure, bool registerAsTool = true)`
- `AddGroup(string label, string toolName, string description, Action<DebugGroupBuilder> configure)`
- `AddFoldout(string label, Action<DebugGroupBuilder> configure)`：兼容旧 debug-ui 的折叠分组入口，不自动注册为 EvalTool 子工具。
- `AddHorizontalGroup(...)`
- `AddVerticalGroup(...)`

工具名和说明必须同时填写。工具名使用 `EvalToolRegistry` 的规则校验。

`DebugEvalToolBuilder` 提供 `AddGroup`、`AddReadOnly`、`AddWritable`、`AddButton` 与 `AddDestructiveButton`。`AddWritable(..., EvalToolSafety safety)` 和 `AddButton(..., EvalToolSafety safety)` 接收 UnityEvalTool 的完整安全标记：本地存档/设置使用 `PersistsData`，异步长流程使用 `LongRunning`，场景、工程、Editor、网络和重载影响使用各自对应标记。破坏性动作必须同时声明 `RequiresConfirmation`。

内嵌 Command Line 每次只提交一行；本地 `help` 只列出适用于该页面的语义。电脑级 `unity connect`、stdin、heredoc 与外部 REPL exit 明确不可用；输入 `exit` 会说明这一点，不再静默忽略。

## 说明

- 本包不依赖 `com.annulusgames.debug-ui`、`com.anupackages.debugconsole`、`com.tayx.graphy`，也不依赖项目里的旧 `DebugPanel`。
- 固定结构的运行时 UI 由 UXML/USS 驱动，需要显式放置本包的 `Runtime/Core/Prefabs/DebugPanel.prefab` 来决定是否启用调试面板。
- `DebugPanel` 要求 `UIDocument.panelSettings` 已正确配置；缺失配置会直接报错，不再运行时静默补一个临时配置。
- 内置字段 UI 支持 `bool`、数字基础类型、`string`、枚举、`Vector2/3/4`、`Vector2Int/3Int`、`Rect/RectInt`、`Bounds/BoundsInt`。这些类型都可通过 `AddField<T>`、`AddReadOnly<T>` 或 `AddValue<T>` 进入对应 UI Toolkit 字段。其他值类型只要 getter / setter 支持，仍可通过 EvalTool 暴露，并在窗口内按只读文本显示。
