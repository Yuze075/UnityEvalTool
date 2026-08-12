# Advanced usage

## Eval contract

The Broker `eval` tool accepts the same Unity-side program as before:

```javascript
async function execute() {
  const runtime = await import('tools://Runtime');
  return runtime.getState();
}
```

Use `tools://` to discover root tools, `tools://<Tool/Path>` to import one, and direct
PuerTS `CS.*` interop only when no helper covers the operation. Results must remain
JSON-serializable.

## Session behavior

Each connection handle maps to `mcp:<handle>` in Unity; each interactive terminal maps to
`cli:<consoleId>`. Repeated calls reuse that PuerTS VM until the handle/console is released,
Unity reloads its scripting domain, or `resetSession` is requested. A VM generation change
is visible in status and the CLI console.

The Broker releases a CLI VM when its console closes and releases an MCP VM when its
connection lease expires. A release that coincides with Domain Reload is retained for the
same Unity process and delivered after reconnection. If the Broker itself restarts, Unity
detects the new `brokerInstanceId` and disposes all sessions owned by the previous process.

## Safe compilation flow

1. Call `unity_status`, retain its `capturedAtUtc`, and call `unity_connect`.
2. Use eval to edit code and call `Editor.scheduleAssetRefresh()`.
3. The Unity client reports `Compiling` and then `Reloading`; the transport may disconnect.
4. Call `unity_status` with the known `instanceId`, `waitFor: "compilation-complete"`,
   `observedAfterUtc` set to the retained pre-request `capturedAtUtc`, and a sufficient
   timeout. This marker prevents a stale `Ready` sample from winning the race before Unity
   publishes `Compiling`. The wait runs in the Broker.
5. Inspect compiler counts/phase. Query status again and reconnect if the registry revision
   changed before the next eval.

Do not retry an eval whose connection was interrupted after dispatch. The Broker reports
whether execution outcome may be unknown.

## CLI parser

The native CLI forwards ordinary input to `EvalCliCommandService`, so global help, tool
help, aliases, quoted arguments, `eval-js`, log streaming, and tool commands retain their
existing behavior. Tool paths are shown by `unity tools`; use the displayed casing.

## Failure classes

- `RegistryChanged`: repeat status discovery before connecting.
- `UnityBusy`: wait through status rather than looping eval.
- `CompilationFailed`: read compiler counts/logs and fix the compile error.
- `UnityDisconnected`: wait for the retained instance to reconnect.
- `ConnectionHandleInvalid`: the lease expired or the Unity process was replaced; discover
  and connect again.
- `ExecutionOutcomeUnknown`: inspect Unity state before deciding whether a mutating action
  should be repeated.
