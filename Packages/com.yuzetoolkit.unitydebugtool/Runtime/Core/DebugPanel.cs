#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-9000)]
    public sealed class DebugPanel : MonoBehaviour
    {
        private static DebugPanel? _instance;

        [SerializeField, Tooltip("Whether the debug panel is visible immediately after it is created.")]
        private bool showOnStartup;

        [SerializeField, Tooltip("Whether Ctrl must be held when pressing the toggle key.")]
        private bool toggleCtrl;

        [SerializeField, Tooltip("Whether Alt must be held when pressing the toggle key.")]
        private bool toggleAlt;

        private readonly List<IDebugPanelModule> _modules = new();
        private readonly Dictionary<IDebugPanelModule, bool> _moduleVisibility = new();
        private UIDocument? _uiDocument;
        private VisualElement? _root;
        private DebugPanelContext? _context;
        private bool _modulesInitialized;
        private bool _startupVisibilityApplied;
        private bool _visible;

        public static DebugPanel? ActiveInstance => _instance;

        public static bool IsActive => _instance != null;

        public bool IsVisible
        {
            get => _visible;
            set => SetVisible(value);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _uiDocument ??= GetComponent<UIDocument>();
            InitializeDocument();
        }

        private void OnEnable()
        {
            InitializeDocument();
        }

        private void OnDisable()
        {
            if (_instance != this) return;
            ShutdownModules();
        }

        private void Update()
        {
            if (_root == null || !_modulesInitialized)
                InitializeDocument();
            if (_root == null) return;

            HandleToggleInput();

            if (!_visible) return;

            for (var i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                if (IsModuleVisible(module))
                    module.Tick();
            }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            ShutdownModules();
            _instance = null;
        }

        private void InitializeDocument()
        {
            _uiDocument ??= GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                Debug.LogError($"{nameof(DebugPanel)} requires a {nameof(UIDocument)} component.", this);
                enabled = false;
                return;
            }

            if (_uiDocument.panelSettings == null)
            {
                Debug.LogError($"{nameof(DebugPanel)} requires configured {nameof(PanelSettings)} on its {nameof(UIDocument)}.", this);
                enabled = false;
                return;
            }

            if (_root == null)
            {
                _root = _uiDocument.rootVisualElement;
                if (_root == null) return;

                _root.Clear();
                PrepareRoot(_root);
                _context = new DebugPanelContext(_root);
            }

            if (!_modulesInitialized)
                InitializeModules();

            if (!_startupVisibilityApplied)
            {
                _startupVisibilityApplied = true;
                SetAllModulesVisible(showOnStartup);
            }
            else
            {
                UpdateRootVisibility();
            }
        }

        private static void PrepareRoot(VisualElement root)
        {
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.style.flexGrow = 1;
        }

        private void InitializeModules()
        {
            if (_context == null) return;

            _modules.Clear();
            _moduleVisibility.Clear();
            foreach (var module in GetComponents<MonoBehaviour>()
                         .OfType<IDebugPanelModule>()
                         .OrderBy(module => module.SortOrder))
            {
                module.Initialize(_context);
                _modules.Add(module);
                _moduleVisibility[module] = false;
            }

            _modulesInitialized = true;
        }

        private void ShutdownModules()
        {
            if (!_modulesInitialized) return;

            for (var i = _modules.Count - 1; i >= 0; i--)
                _modules[i].Shutdown();

            _modules.Clear();
            _moduleVisibility.Clear();
            _modulesInitialized = false;
            _visible = false;
        }

        private void SetVisible(bool visible)
        {
            SetAllModulesVisible(visible);
        }

        private void SetAllModulesVisible(bool visible)
        {
            for (var i = 0; i < _modules.Count; i++)
                SetModuleVisible(_modules[i], visible);

            UpdateRootVisibility();
        }

        private void HandleToggleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var pressedKeys = new List<Key>();
            for (var i = 0; i < _modules.Count; i++)
            {
                var toggleKey = _modules[i].ToggleKey;
                if (pressedKeys.Contains(toggleKey)) continue;
                if (IsTogglePressed(keyboard, toggleKey))
                    pressedKeys.Add(toggleKey);
            }

            for (var i = 0; i < pressedKeys.Count; i++)
                ToggleModulesByKey(pressedKeys[i]);
        }

        private void ToggleModulesByKey(Key toggleKey)
        {
            var anyVisible = false;
            for (var i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                if (module.ToggleKey == toggleKey && IsModuleVisible(module))
                {
                    anyVisible = true;
                    break;
                }
            }

            var visible = !anyVisible;
            for (var i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                if (module.ToggleKey == toggleKey)
                    SetModuleVisible(module, visible);
            }

            UpdateRootVisibility();
        }

        private void SetModuleVisible(IDebugPanelModule module, bool visible)
        {
            _moduleVisibility[module] = visible;
            module.SetVisible(visible);
        }

        private bool IsModuleVisible(IDebugPanelModule module)
        {
            return _moduleVisibility.TryGetValue(module, out var visible) && visible;
        }

        private void UpdateRootVisibility()
        {
            _visible = _modules.Any(IsModuleVisible);
            if (_root == null) return;

            _root.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            _root.pickingMode = PickingMode.Ignore;
        }

        private bool IsTogglePressed(Keyboard keyboard, Key toggleKey)
        {
            if (toggleKey == Key.None) return false;

            var ctrlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            var altPressed = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
            return (!toggleCtrl || ctrlPressed) &&
                   (!toggleAlt || altPressed) &&
                   keyboard[toggleKey].wasPressedThisFrame;
        }
    }
}
