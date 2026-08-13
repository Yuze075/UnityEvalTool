#nullable enable
using UnityEngine.InputSystem;

namespace YuzeToolkit
{
    public interface IDebugPanelModule
    {
        int SortOrder { get; }

        Key ToggleKey { get; }

        void Initialize(DebugPanelContext context);

        void SetVisible(bool visible);

        void Tick();

        void Shutdown();
    }
}
