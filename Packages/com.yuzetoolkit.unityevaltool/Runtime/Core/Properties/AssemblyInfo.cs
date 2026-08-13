using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("UnityEvalTool.Editor")]
[assembly: InternalsVisibleTo("UnityEvalTool.Tools")]
[assembly: InternalsVisibleTo("UnityEvalTool.Editor.Tools")]
#if UNITY_INCLUDE_TESTS
[assembly: InternalsVisibleTo("UnityEvalTool.Tests.Editor")]
#endif
