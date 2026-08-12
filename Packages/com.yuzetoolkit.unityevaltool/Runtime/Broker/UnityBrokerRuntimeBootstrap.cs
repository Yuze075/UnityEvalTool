#nullable enable
using UnityEngine;

namespace YuzeToolkit
{
    internal static class UnityBrokerRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
#if UNITY_EDITOR
            return;
#else
            var gameObject = new GameObject("UnityEvalTool Broker Client")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<UnityBrokerRuntimeRunner>();
            UnityBrokerClient.Shared.Start();
#endif
        }
    }

    internal sealed class UnityBrokerRuntimeRunner : MonoBehaviour
    {
        private void Update() => UnityBrokerClient.Shared.Tick();
        private void OnApplicationQuit() => UnityBrokerClient.Shared.PublishExitingAndStop();
    }
}
