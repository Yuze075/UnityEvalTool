#nullable enable
using System;
using UnityEditor;
using UnityEngine;

namespace YuzeToolkit
{
    internal static class EditorBrokerBootstrap
    {
        private const double StatusTickIntervalSeconds = 0.5;
        private const string KeyInstanceId = nameof(YuzeToolkit) + ".Broker.InstanceId";
        private const string KeyConnectionEpoch = nameof(YuzeToolkit) + ".Broker.ConnectionEpoch";
        private static double _nextStatusTickAt;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (!EditorProcessGuard.IsPrimaryEditorProcess) return;
            EditorBrokerStatusMonitor.Initialize();
            EditorApplication.delayCall -= ApplyPersistedToolStates;
            EditorApplication.delayCall += ApplyPersistedToolStates;
            var instanceId = SessionState.GetString(KeyInstanceId, string.Empty);
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                instanceId = Guid.NewGuid().ToString("N");
                SessionState.SetString(KeyInstanceId, instanceId);
            }
            var epoch = SessionState.GetInt(KeyConnectionEpoch, 0) + 1;
            SessionState.SetInt(KeyConnectionEpoch, epoch);
            var identity = new BrokerClientIdentity
            {
                InstanceId = instanceId,
                ConnectionEpoch = epoch,
                VmGeneration = epoch
            };
            UnityBrokerClient.Shared.Configure(identity, EditorBrokerProcessLauncher.EnsureRunning);

            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            EditorApplication.quitting -= OnQuitting;
            EditorApplication.quitting += OnQuitting;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;

            Update();
            UnityBrokerClient.Shared.Start();
        }

        private static void ApplyPersistedToolStates()
        {
            const string prefix = nameof(YuzeToolkit) + ".McpTool.Enabled.";
            foreach (var tool in EvalToolRegistry.ListTools(true))
            {
                if (!EditorPrefs.HasKey(prefix + tool.Path)) continue;
                EvalToolRegistry.SetEnabled(tool.Path, EditorPrefs.GetBool(prefix + tool.Path, true));
            }
        }

        private static void Update()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextStatusTickAt) return;
            _nextStatusTickAt = now + StatusTickIntervalSeconds;
            var generation = UnityBrokerClient.Shared.Identity.VmGeneration;
            UnityBrokerClient.Shared.Tick(EditorBrokerStatusMonitor.Capture(generation));
        }

        private static void BeforeAssemblyReload()
        {
            EditorBrokerStatusMonitor.MarkReloading();
            var generation = UnityBrokerClient.Shared.Identity.VmGeneration;
            UnityBrokerClient.Shared.Tick(EditorBrokerStatusMonitor.Capture(generation));
            UnityBrokerClient.Shared.PublishReloadingAndStop();
        }

        private static void OnQuitting()
        {
            UnityBrokerClient.Shared.PublishExitingAndStop();
        }
    }
}
