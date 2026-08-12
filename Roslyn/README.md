# UnityEvalToolRoslyn

Roslyn source generator for `com.yuzetoolkit.unityevaltool`.

The generator reads C# tool classes marked with `YuzeToolkit.EvalToolAttribute` and methods marked with `YuzeToolkit.EvalFunctionAttribute`, then emits the partial `IEvalTool` metadata implementation used by `EvalToolRegistry`.

## Build

```bash
dotnet test Roslyn/UnityEvalToolRoslyn.sln -c Release
dotnet build Roslyn/src/UnityEvalTool.SourceGenerator/UnityEvalTool.SourceGenerator.csproj -c Release
```

Unity uses the deployed analyzer DLL at:

```text
Packages/com.yuzetoolkit.unityevaltool/Analyzers/UnityEvalTool.SourceGenerator.dll
```

After rebuilding, copy the release DLL to that path and keep the `.meta` file with the `RoslynAnalyzer` label.
The release workflow rebuilds the generator and rejects a release if the committed analyzer
binary differs from that source build.

The solution is committed as ordinary source beside `Broker`; the Unity package does not
carry or download a Roslyn source zip.
