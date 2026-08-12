# 进阶使用

## Eval 契约

Broker 的 `eval` 工具继续接收原有 Unity 侧程序：

```javascript
async function execute() {
  const runtime = await import('tools://Runtime');
  return runtime.getState();
}
```

用 `tools://` 发现根工具，用 `tools://<Tool/Path>` 导入具体工具；只有 helper 不覆盖
操作时才直接使用 PuerTS `CS.*`。返回值必须可以 JSON 序列化。

## Session 行为

每个连接 handle 在 Unity 中对应 `mcp:<handle>`，每个交互终端对应
`cli:<consoleId>`。后续调用复用同一 PuerTS VM，直到 handle/console 被释放、Unity
重载脚本域或请求 `resetSession`。状态和 CLI 控制台会显示 VM generation 变化。

## 安全编译流程

1. 调用 `unity_status`，保留其 `capturedAtUtc`，再调用 `unity_connect`。
2. 用 eval 修改代码并调用 `Editor.scheduleAssetRefresh()`。
3. Unity 客户端报告 `Compiling`、随后 `Reloading`；传输可能暂时断开。
4. 使用已知 `instanceId` 调用 `unity_status`，设置
   `waitFor: "compilation-complete"`，把请求前保留的 `capturedAtUtc` 作为
   `observedAfterUtc`，并设置足够 timeout。该标记会避免 Unity 尚未发布 `Compiling`
   时旧 `Ready` 快照提前结束等待；实际等待发生在 Broker 中。
5. 检查编译器计数与 phase。下一次 eval 前，如果 registry revision 已改变，重新查询
   状态并连接。

已经派发后连接中断的 eval 不得直接重试。Broker 会标明执行结果是否可能不确定。

## CLI 解析

原生 CLI 会把普通输入交给 `EvalCliCommandService`，因此全局帮助、工具帮助、别名、
引号参数、`eval-js`、日志流和工具命令保持原有行为。`unity tools` 会显示工具路径，
命令应使用其显示的大小写。

## 常见失败类型

- `RegistryChanged`：重新查询状态，再连接。
- `UnityBusy`：通过状态等待，不要循环调用 eval。
- `CompilationFailed`：读取编译计数/日志并修复编译错误。
- `UnityDisconnected`：等待 Broker 中保留的实例重连。
- `ConnectionHandleInvalid`：租约过期或 Unity 进程被替换；重新发现和连接。
- `ExecutionOutcomeUnknown`：先检查 Unity 状态，再决定是否重复修改操作。
