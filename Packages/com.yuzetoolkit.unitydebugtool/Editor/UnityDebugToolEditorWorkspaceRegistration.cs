#nullable enable
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YuzeToolkit.UnityAgent;

namespace YuzeToolkit.UnityDebugTool.Editor
{
    [InitializeOnLoad]
    internal static class UnityDebugToolEditorWorkspaceRegistration
    {
        private const string SystemInfoRoot =
            "Packages/com.yuzetoolkit.unitydebugtool/Runtime/SystemInfo/UI/SystemInfo";
        private const string PerformanceRoot =
            "Packages/com.yuzetoolkit.unitydebugtool/Runtime/Performance/UI/PerformanceMonitor";

        private static IDisposable? _systemInfoRegistration;
        private static IDisposable? _performanceRegistration;

        static UnityDebugToolEditorWorkspaceRegistration()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += RegisterEditModeSections;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    DisposeRegistrations();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    EditorApplication.delayCall += RegisterEditModeSections;
                    break;
            }
        }

        private static void RegisterEditModeSections()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || _systemInfoRegistration != null) return;

            var systemTemplate = LoadRequired<VisualTreeAsset>(SystemInfoRoot + ".uxml");
            var systemStyle = LoadRequired<StyleSheet>(SystemInfoRoot + ".uss");
            var performanceTemplate = LoadRequired<VisualTreeAsset>(PerformanceRoot + ".uxml");
            var performanceStyle = LoadRequired<StyleSheet>(PerformanceRoot + ".uss");

            _systemInfoRegistration = UnityAgentWorkspaceRegistry.RegisterSystemInfoSection(
                "unity-debug-tool-system-info", 0,
                () => SystemInfoModule.CreateWorkspaceSection(systemTemplate, systemStyle));
            try
            {
                _performanceRegistration = UnityAgentWorkspaceRegistry.RegisterSystemInfoSection(
                    "unity-debug-tool-performance", 10,
                    () => PerformanceMonitorModule.CreateWorkspaceSection(performanceTemplate, performanceStyle));
            }
            catch
            {
                DisposeRegistrations();
                throw;
            }
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null
                ? asset
                : throw new MissingReferenceException($"Required UnityDebugTool asset was not found: {path}");
        }

        private static void DisposeRegistrations()
        {
            _performanceRegistration?.Dispose();
            _performanceRegistration = null;
            _systemInfoRegistration?.Dispose();
            _systemInfoRegistration = null;
        }
    }
}
