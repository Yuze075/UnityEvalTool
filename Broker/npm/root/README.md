# @yuzetoolkit/unityevaltool

Native C# Broker, MCP server and `unity` CLI for [UnityEvalTool](https://github.com/Yuze075/UnityEvalTool).

## Install

```bash
npm install --global @yuzetoolkit/unityevaltool
unity doctor
```

The install selects the matching NativeAOT package for macOS, Linux or Windows on x64 or arm64, then installs a current-user background service bound to `127.0.0.1:2347`.

The Unity-side package must also be installed in each project:

```text
https://github.com/Yuze075/UnityEvalTool.git?path=/Packages/com.yuzetoolkit.unityevaltool#v2.0.0
```

## CLI

```bash
unity list
unity
unity connect <instance-id>
unity Runtime getState
unity eval-js --code "return 1 + 2;"
unity service status
```

## MCP

Connect a Streamable HTTP MCP client to `http://127.0.0.1:2347/mcp`. Read the generated token from `~/.unityevaltool/auth.json` and send it as `Authorization: Bearer <token>`.

The MCP tools are `unity_status`, `unity_connect` and `eval`. Discovery and selection are mandatory before eval. See the [full documentation](https://github.com/Yuze075/UnityEvalTool#readme) and [protocol specification](https://github.com/Yuze075/UnityEvalTool/blob/main/Packages/com.yuzetoolkit.unityevaltool/docs/BROKER_PROTOCOL_2.md).

## Service

```bash
unity service install
unity service start
unity service stop
unity service restart
unity service status
unity service uninstall
```

The service is installed for the current user only: LaunchAgent on macOS, systemd user unit on Linux, and Scheduled Task on Windows.

License: MIT.
