#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YuzeToolkit.UnityAgent
{
    internal static class AgentRuntimeConsoleRegistration
    {
        private static IDisposable? _registration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistrationHandle()
        {
            _registration = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            _registration?.Dispose();
            _registration = RuntimeConsoleTabRegistry.Register("unity-agent-tool", CreateTabs);
        }

        private static IEnumerable<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context)
        {
            return new IRuntimeConsoleTab[] { new AgentWorkbenchRuntimeConsoleTab(UnityAgentHost.Default) };
        }
    }

    internal sealed class AgentWorkbenchRuntimeConsoleTab : RuntimeConsoleTabBase
    {
        private readonly UnityAgentWorkbenchView _view;

        public AgentWorkbenchRuntimeConsoleTab(UnityAgentHost host) : base("unity-agent", "Unity Agent", 80)
        {
            _view = new UnityAgentWorkbenchView(host, AgentRuntimeConsoleUi.CreateAgentScrollContainerForConsole);
            Root.Add(_view);
        }

        public override void Tick()
        {
            _view.Tick();
        }

        public override void Shutdown()
        {
            _view.Dispose();
            base.Shutdown();
        }
    }

    internal static class AgentRuntimeConsoleUi
    {
        public static AgentScrollContainer CreateAgentScrollContainerForConsole()
        {
            var panView = RuntimeConsoleUi.CreatePanView();
            return new AgentScrollContainer(panView.Root, panView.Content, panView.ScrollToEnd);
        }
    }
}
