#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit.UnityAgent
{
    public enum UnityAgentWorkbenchPage
    {
        Chat,
        Settings
    }

    public sealed class AgentScrollContainer
    {
        private readonly Action _scrollToEnd;

        public AgentScrollContainer(VisualElement root, VisualElement content, Action scrollToEnd)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            _scrollToEnd = scrollToEnd ?? throw new ArgumentNullException(nameof(scrollToEnd));
        }

        public VisualElement Root { get; }
        public VisualElement Content { get; }
        public void ScrollToEnd() => _scrollToEnd();

        public static AgentScrollContainer CreateDefault()
        {
            var scroll = AgentUi.Scroll(ScrollViewMode.Vertical);
            return new AgentScrollContainer(scroll, scroll.contentContainer,
                () => scroll.schedule.Execute(() => scroll.scrollOffset =
                    new Vector2(scroll.scrollOffset.x, scroll.contentContainer.layout.height)));
        }
    }

    public sealed class UnityAgentWorkbenchView : VisualElement, IDisposable
    {
        private readonly UnityAgentHost _host;
        private readonly Func<AgentScrollContainer> _scrollFactory;
        private readonly VisualElement _pageHost;
        private readonly AgentModalLayer _modal;
        private IDisposable? _page;
        private UnityAgentWorkbenchPage _pageKind;
        private bool _disposed;

        public UnityAgentWorkbenchView(
            UnityAgentHost host,
            Func<AgentScrollContainer>? scrollFactory = null,
            UnityAgentWorkbenchPage initialPage = UnityAgentWorkbenchPage.Chat)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _scrollFactory = scrollFactory ?? AgentScrollContainer.CreateDefault;
            style.flexGrow = 1;
            style.minWidth = 0;
            style.minHeight = 0;
            style.backgroundColor = AgentUi.Background;
            style.color = AgentUi.Text;

            _pageHost = new VisualElement { name = "unity-agent-page-host" };
            _pageHost.style.flexGrow = 1;
            _pageHost.style.minWidth = 0;
            _pageHost.style.minHeight = 0;
            Add(_pageHost);

            _modal = new AgentModalLayer();
            Add(_modal);
            ShowPage(initialPage);
        }

        public void ShowPage(UnityAgentWorkbenchPage page)
        {
            if (_disposed) return;
            _page?.Dispose();
            _pageHost.Clear();
            _pageKind = page;
            if (page == UnityAgentWorkbenchPage.Chat)
            {
                var chat = new AgentChatView(_host, _scrollFactory(),
                    () => ShowPage(UnityAgentWorkbenchPage.Settings), ShowError, ShowConfirmation);
                _page = chat;
                _pageHost.Add(chat);
            }
            else
            {
                var settings = new AgentSettingsView(_host, _scrollFactory(),
                    () => ShowPage(UnityAgentWorkbenchPage.Chat), ShowError, ShowConfirmation);
                _page = settings;
                _pageHost.Add(settings);
            }
        }

        public void Tick()
        {
            if (_disposed) return;
            if (_page is AgentChatView chat) chat.Tick();
            else if (_page is AgentSettingsView settings) settings.Tick();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _page?.Dispose();
            _page = null;
        }

        private void ShowError(string title, string message) => _modal.ShowError(title, message);

        private void ShowConfirmation(string title, string message, Action confirmed) =>
            _modal.ShowConfirmation(title, message, confirmed);
    }

    public sealed class AgentChatView : VisualElement, IDisposable
    {
        private readonly UnityAgentHost _host;
        private readonly Action _openSettings;
        private readonly Action<string, string> _showError;
        private readonly Action<string, string, Action> _showConfirmation;
        private VisualElement _sessionList = new();
        private readonly VisualElement _messageList;
        private readonly AgentScrollContainer _messageScroll;
        private readonly AgentChoiceField _provider;
        private readonly AgentEditableChoiceField _model;
        private readonly AgentChoiceField _effort;
        private readonly AgentChoiceField _permission;
        private readonly Label _status;
        private readonly AgentTextField _composer;
        private readonly AgentButton _action;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Dictionary<string, IReadOnlyList<string>> _modelChoices = new(StringComparer.Ordinal);
        private readonly HashSet<string> _discoveryStartedProfiles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _profileIdsByLabel = new(StringComparer.Ordinal);
        private long _lastRevision = -1;
        private string _selectedSessionId = string.Empty;
        private string _shownSessionError = string.Empty;
        private bool _archiveCollapsed = true;
        private bool _initialized;
        private bool _disposed;

        public AgentChatView(
            UnityAgentHost host,
            AgentScrollContainer? messageScroll = null,
            Action? openSettings = null,
            Action<string, string>? showError = null,
            Action<string, string, Action>? showConfirmation = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _openSettings = openSettings ?? (() => { });
            _showError = showError ?? ((_, message) => Debug.LogError(message));
            _showConfirmation = showConfirmation ?? ((_, _, confirmed) => confirmed());
            name = "unity-agent-chat-view";
            style.flexGrow = 1;
            style.minWidth = 0;
            style.minHeight = 0;
            style.flexDirection = FlexDirection.Row;

            var sidebar = CreateSidebar();
            Add(sidebar);

            var main = new VisualElement { name = "unity-agent-chat-main" };
            main.style.flexGrow = 1;
            main.style.minWidth = 0;
            main.style.minHeight = 0;
            main.style.alignItems = Align.Stretch;
            Add(main);

            var header = new VisualElement();
            header.style.height = 54;
            header.style.flexShrink = 0;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 20;
            header.style.paddingRight = 20;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = AgentUi.Border;
            var title = new Label("Agent conversation");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);
            _status = new Label("Loading…");
            _status.style.flexGrow = 1;
            _status.style.unityTextAlign = TextAnchor.MiddleRight;
            _status.style.color = AgentUi.Muted;
            header.Add(_status);
            main.Add(header);

            _messageScroll = messageScroll ?? AgentScrollContainer.CreateDefault();
            _messageScroll.Root.style.flexGrow = 1;
            _messageScroll.Root.style.minHeight = 0;
            _messageScroll.Content.style.paddingLeft = 24;
            _messageScroll.Content.style.paddingRight = 24;
            _messageScroll.Content.style.paddingTop = 18;
            _messageScroll.Content.style.paddingBottom = 16;
            _messageList = _messageScroll.Content;
            main.Add(_messageScroll.Root);

            var composer = AgentUi.RoundedPanel(20);
            composer.style.flexShrink = 0;
            composer.style.marginLeft = 22;
            composer.style.marginRight = 22;
            composer.style.marginBottom = 18;
            composer.style.paddingLeft = 12;
            composer.style.paddingRight = 10;
            composer.style.paddingTop = 10;
            composer.style.paddingBottom = 10;
            composer.style.backgroundColor = AgentUi.Composer;
            AgentUi.SetBorder(composer, AgentUi.BorderStrong, 1);
            main.Add(composer);

            _composer = new AgentTextField(surface: false)
            {
                multiline = true,
                Placeholder = "Describe a task or ask a question…"
            };
            AgentTooltip.Attach(_composer, "Describe a task. Press Ctrl/Cmd+Enter to send.");
            _composer.style.minHeight = 52;
            _composer.style.maxHeight = 180;
            _composer.style.whiteSpace = WhiteSpace.Normal;
            _composer.RegisterValueChangedCallback(_ => RefreshActionButton());
            _composer.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return || !evt.ctrlKey && !evt.commandKey) return;
                RunUiTask(ActAsync);
                evt.StopPropagation();
            });
            composer.Add(_composer);

            var controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.flexWrap = Wrap.Wrap;
            controls.style.alignItems = Align.Center;
            controls.style.marginTop = 5;
            composer.Add(controls);

            _permission = AgentUi.CompactDropdown(new[]
            {
                AgentPermissionMode.FullAccess.ToString(), AgentPermissionMode.ConfirmWrites.ToString()
            }, "Execution permission");
            _permission.style.width = 128;
            _permission.ValueFormatter = value => value == AgentPermissionMode.FullAccess.ToString()
                ? "◉  Full access"
                : "◉  Confirm writes";
            _permission.SetForeground(AgentUi.Accent);
            _permission.RegisterValueChangedCallback(_ => SaveConversationSelection());
            controls.Add(_permission);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            spacer.style.minWidth = 14;
            controls.Add(spacer);
            _provider = AgentUi.CompactDropdown(Array.Empty<string>(), "API provider profile");
            _provider.style.width = 154;
            _provider.RegisterValueChangedCallback(_ =>
            {
                RefreshCuratedModels();
                RunUiTask(RefreshModelsAsync);
                SaveConversationSelection();
            });
            controls.Add(_provider);
            _model = new AgentEditableChoiceField(string.Empty,
                "Choose a discovered model or type an exact model id.");
            _model.style.width = 190;
            _model.style.flexGrow = 0;
            _model.ValueCommitted += SaveConversationSelection;
            _model.ChoiceSelected += value =>
            {
                ApplyChatModelOptions(value);
                SaveConversationSelection();
            };
            controls.Add(_model);
            _effort = AgentUi.CompactDropdown(new[] { "default", "none", "low", "medium", "high", "xhigh" },
                "Reasoning effort");
            _effort.style.width = 104;
            _effort.RegisterValueChangedCallback(_ => SaveConversationSelection());
            controls.Add(_effort);
            _action = AgentUi.IconButton("↑", "Send", () => RunUiTask(ActAsync), 36,
                AgentUi.Send, AgentUi.SendForeground);
            _action.style.marginLeft = 7;
            controls.Add(_action);

            RunUiTask(InitializeAsync);
        }

        private VisualElement CreateSidebar()
        {
            var sidebar = new VisualElement { name = "unity-agent-sidebar" };
            sidebar.style.width = 246;
            sidebar.style.minWidth = 190;
            sidebar.style.flexShrink = 0;
            sidebar.style.paddingLeft = 10;
            sidebar.style.paddingRight = 10;
            sidebar.style.paddingTop = 12;
            sidebar.style.paddingBottom = 10;
            sidebar.style.borderRightWidth = 1;
            sidebar.style.borderRightColor = AgentUi.Border;
            sidebar.style.backgroundColor = AgentUi.Sidebar;

            var brand = new Label("Unity Agent");
            brand.style.fontSize = 19;
            brand.style.unityFontStyleAndWeight = FontStyle.Bold;
            brand.style.marginLeft = 5;
            brand.style.marginBottom = 11;
            sidebar.Add(brand);
            var newConversation = AgentUi.Button("＋  New conversation", "Create an independent conversation.",
                () => RunUiTask(CreateSessionAsync), 0, AgentUi.Panel);
            newConversation.style.flexGrow = 0;
            sidebar.Add(newConversation);

            var listScroll = AgentUi.Scroll(ScrollViewMode.Vertical);
            listScroll.style.flexGrow = 1;
            listScroll.style.minHeight = 0;
            listScroll.style.marginTop = 8;
            _sessionList = listScroll.contentContainer;
            sidebar.Add(listScroll);

            var settings = AgentUi.Button("⚙  Settings", "Open API and Agent settings.", _openSettings, 0, AgentUi.Transparent);
            settings.style.flexGrow = 0;
            settings.style.unityTextAlign = TextAnchor.MiddleLeft;
            settings.style.marginTop = 8;
            sidebar.Add(settings);
            return sidebar;
        }

        public void Tick()
        {
            if (_disposed) return;
            var revision = _host.Revision;
            if (!_initialized || revision == _lastRevision) return;
            _lastRevision = revision;
            Refresh();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async Task InitializeAsync()
        {
            await _host.EnsureInitializedAsync(_lifetime.Token);
            var sessions = _host.GetSessions();
            _selectedSessionId = sessions.Count == 0
                ? (await _host.CreateSessionAsync(_lifetime.Token)).Id
                : sessions.OrderBy(value => value.IsArchived)
                    .ThenByDescending(value => value.IsPinned)
                    .ThenByDescending(value => value.UpdatedAtUtc).First().Id;
            _initialized = true;
            _lastRevision = -1;
            var current = CurrentSession();
            if (current != null) await DiscoverSessionModelsOnceAsync(current.ProviderProfileId);
        }

        private async Task CreateSessionAsync()
        {
            _selectedSessionId = (await _host.CreateSessionAsync(_lifetime.Token)).Id;
            _lastRevision = -1;
        }

        private async Task DeleteSessionAsync(string sessionId)
        {
            var session = _host.GetSession(sessionId);
            if (session == null) return;
            if (session.State is AgentSessionState.Running or AgentSessionState.AwaitingApproval)
                throw new InvalidOperationException("Stop the active conversation before deleting it.");
            await _host.DeleteSessionAsync(sessionId, _lifetime.Token);
            var sessions = _host.GetSessions();
            _selectedSessionId = sessions.Count > 0
                ? sessions.OrderBy(value => value.IsArchived)
                    .ThenByDescending(value => value.UpdatedAtUtc).First().Id
                : (await _host.CreateSessionAsync(_lifetime.Token)).Id;
        }

        private Task ActAsync()
        {
            var current = CurrentSession();
            var hasText = !string.IsNullOrWhiteSpace(_composer.value);
            if (hasText)
            {
                if (current != null && IsActive(current))
                    return InterruptAndSendAsync(current.Id, _composer.value.Trim());
                return SendAsync();
            }
            if (current != null && IsActive(current)) _host.StopSession(current.Id);
            return Task.CompletedTask;
        }

        private async Task InterruptAndSendAsync(string sessionId, string text)
        {
            _host.StopSession(sessionId);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                _lifetime.Token.ThrowIfCancellationRequested();
                var session = _host.GetSession(sessionId);
                if (session == null) throw new InvalidOperationException("The conversation no longer exists.");
                if (!IsActive(session))
                {
                    if (_selectedSessionId != sessionId)
                        throw new InvalidOperationException("The active conversation changed before the message could be sent.");
                    await SaveConversationSelectionAsync(sessionId);
                    if (string.Equals(_composer.value.Trim(), text, StringComparison.Ordinal))
                        _composer.value = string.Empty;
                    await _host.SendMessageAsync(sessionId, text, _lifetime.Token);
                    return;
                }
                await Task.Delay(50, _lifetime.Token);
            }
            throw new TimeoutException("The active Agent turn did not stop within 10 seconds.");
        }

        private async Task SendAsync()
        {
            var text = _composer.value.Trim();
            if (string.IsNullOrEmpty(_selectedSessionId) || text.Length == 0) return;
            var sessionId = _selectedSessionId;
            await SaveConversationSelectionAsync(sessionId);
            _composer.value = string.Empty;
            await _host.SendMessageAsync(sessionId, text, _lifetime.Token);
        }

        private void SaveConversationSelection()
        {
            if (_initialized) RunUiTask(() => SaveConversationSelectionAsync(_selectedSessionId));
        }

        private async Task SaveConversationSelectionAsync(string sessionId)
        {
            var settings = _host.Settings;
            var profile = _profileIdsByLabel.TryGetValue(_provider.value, out var profileId)
                ? settings.ProviderProfiles.FirstOrDefault(value => value.Id == profileId)
                : null;
            if (profile == null || string.IsNullOrWhiteSpace(sessionId)) return;
            var permission = Enum.TryParse<AgentPermissionMode>(_permission.value, out var parsed)
                ? parsed
                : AgentPermissionMode.FullAccess;
            var effort = _effort.value == "default" ? string.Empty : _effort.value;
            await _host.UpdateSessionAsync(sessionId, profile.Id, _model.Value, effort, permission,
                _lifetime.Token);
        }

        private async Task RefreshModelsAsync()
        {
            var profile = ResolveSelectedProfile();
            if (profile == null) return;
            try
            {
                var discovery = await _host.DiscoverModelsAsync(profile, _lifetime.Token);
                var models = discovery.Models.Select(value => value.Id).ToList();
                _modelChoices[profile.Id] = models;
                _model.SetChoices(models);
                if (string.IsNullOrWhiteSpace(_model.Value) && models.Count > 0) _model.SetValue(models[0]);
                ApplyChatModelOptions(_model.Value);
                if (!string.IsNullOrWhiteSpace(discovery.Warning))
                    _showError("Using curated model defaults", discovery.Warning);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _model.SetChoices(string.IsNullOrWhiteSpace(profile.Model)
                    ? Array.Empty<string>()
                    : new[] { profile.Model });
                _showError("Model discovery failed", exception.Message +
                    "\n\nYou can still type an exact model id in the model field.");
            }
        }

        private async Task DiscoverSessionModelsOnceAsync(string profileId)
        {
            if (!_discoveryStartedProfiles.Add(profileId)) return;
            var profile = _host.Settings.ProviderProfiles.FirstOrDefault(value => value.Id == profileId);
            if (profile == null) return;
            try
            {
                var discovery = await _host.DiscoverModelsAsync(profile, _lifetime.Token);
                var models = discovery.Models.Select(value => value.Id).ToList();
                _modelChoices[profile.Id] = models;
                var current = CurrentSession();
                if (current?.ProviderProfileId == profile.Id)
                {
                    _model.SetChoices(models);
                    var selectedModel = !string.IsNullOrWhiteSpace(current.Model)
                        ? current.Model
                        : !string.IsNullOrWhiteSpace(profile.Model)
                            ? profile.Model
                            : models.FirstOrDefault() ?? string.Empty;
                    _model.SetValueWithoutNotify(selectedModel);
                    ApplyChatModelOptions(_model.Value);
                    if (string.IsNullOrWhiteSpace(current.Model) && !string.IsNullOrWhiteSpace(selectedModel))
                    {
                        await _host.UpdateSessionAsync(current.Id, profile.Id, selectedModel,
                            string.IsNullOrWhiteSpace(current.ReasoningEffort)
                                ? profile.ReasoningEffort
                                : current.ReasoningEffort,
                            current.PermissionMode, _lifetime.Token);
                    }
                    // Automatic discovery commonly falls back before the user has configured a
                    // session key. Keep that expected state visible without interrupting every
                    // newly opened page; explicit discovery still presents its warning modally.
                    AgentTooltip.Attach(_model, discovery.Warning);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _showError("Model discovery failed", exception.Message +
                    "\n\nYou can still type an exact model id in the model field.");
            }
        }

        private void Refresh()
        {
            var sessions = _host.GetSessions();
            if (sessions.Count > 0 && sessions.All(value => value.Id != _selectedSessionId))
                _selectedSessionId = sessions[0].Id;
            RefreshSessionList(sessions);

            var current = CurrentSession();
            if (current == null)
            {
                _messageList.Clear();
                _status.text = "No conversation";
                return;
            }

            var settings = _host.Settings;
            var labels = settings.ProviderProfiles.Select(ProfileLabel).ToList();
            _profileIdsByLabel.Clear();
            foreach (var value in settings.ProviderProfiles) _profileIdsByLabel[ProfileLabel(value)] = value.Id;
            _provider.choices = labels;
            var profile = settings.ProviderProfiles.FirstOrDefault(value => value.Id == current.ProviderProfileId)
                          ?? settings.ProviderProfiles[0];
            _provider.SetValueWithoutNotify(ProfileLabel(profile));
            if (!_discoveryStartedProfiles.Contains(profile.Id))
                RunUiTask(() => DiscoverSessionModelsOnceAsync(profile.Id));
            _model.SetChoices(_modelChoices.TryGetValue(profile.Id, out var discovered)
                ? discovered
                : AgentProviderCatalog.GetModels(profile.ProviderPresetId).Select(value => value.Id));
            var active = IsActive(current);
            _model.SetValueWithoutNotify(string.IsNullOrWhiteSpace(current.Model) ? profile.Model : current.Model);
            _effort.SetValueWithoutNotify(string.IsNullOrWhiteSpace(current.ReasoningEffort)
                ? "default"
                : current.ReasoningEffort);
            if (!_effort.choices.Contains(_effort.value))
            {
                var choices = _effort.choices.ToList();
                choices.Add(_effort.value);
                _effort.choices = choices;
            }
            _permission.SetValueWithoutNotify(current.PermissionMode.ToString());

            _status.text = $"{current.State}  ·  {current.Usage.TotalTokens:N0} tokens";
            _status.style.color = current.State == AgentSessionState.Failed ? AgentUi.Error : AgentUi.Muted;
            _provider.SetEnabled(!active);
            _model.SetEnabled(!active);
            _effort.SetEnabled(!active);
            _permission.SetEnabled(!active);
            RefreshActionButton();

            if (string.IsNullOrWhiteSpace(current.LastError))
                _shownSessionError = string.Empty;
            else if (current.State == AgentSessionState.Failed &&
                     current.LastError.IndexOf("stopped", StringComparison.OrdinalIgnoreCase) < 0 &&
                     current.LastError.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) < 0 &&
                     current.LastError != _shownSessionError)
            {
                _shownSessionError = current.LastError;
                _showError("Agent turn failed", current.LastError);
            }

            _messageList.Clear();
            if (current.Messages.Count == 0)
                _messageList.Add(CreateEmptyState());
            foreach (var message in current.Messages)
                _messageList.Add(CreateMessage(message));
            foreach (var approval in _host.Approvals.Pending.Where(value => value.SessionId == current.Id))
                _messageList.Add(CreateApproval(approval));
            _messageScroll.ScrollToEnd();
        }

        private void RefreshSessionList(IReadOnlyList<AgentSessionDocument> sessions)
        {
            _sessionList.Clear();
            var active = sessions.Where(value => !value.IsArchived).ToList();
            AddSessionSection("PINNED", active.Where(value => value.IsPinned), "", false);

            var groups = _host.Settings.ConversationGroups.OrderBy(value => value.SortOrder).ThenBy(value => value.Name).ToList();
            var validGroupIds = new HashSet<string>(groups.Select(value => value.Id), StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var members = active.Where(value => !value.IsPinned && value.GroupId == group.Id);
                AddSessionSection(group.Name.ToUpperInvariant(), members, group.Id, group.IsCollapsed);
            }
            AddSessionSection("CONVERSATIONS",
                active.Where(value => !value.IsPinned && (string.IsNullOrWhiteSpace(value.GroupId) ||
                                                          !validGroupIds.Contains(value.GroupId))), "", false);
            AddSessionSection("ARCHIVED", sessions.Where(value => value.IsArchived), "__archive", _archiveCollapsed);
        }

        private void AddSessionSection(string title, IEnumerable<AgentSessionDocument> source, string groupId, bool collapsed)
        {
            var sessions = source.OrderBy(value => value.SortOrder).ThenByDescending(value => value.UpdatedAtUtc).ToList();
            if (sessions.Count == 0 && string.IsNullOrEmpty(groupId) && title != "CONVERSATIONS") return;
            var header = new Label((collapsed ? "▸  " : "▾  ") + title);
            header.style.fontSize = 10;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = AgentUi.Muted;
            header.style.marginLeft = 6;
            header.style.marginTop = 10;
            header.style.marginBottom = 4;
            header.style.paddingTop = 4;
            header.style.paddingBottom = 4;
            header.style.paddingLeft = 4;
            header.style.paddingRight = 4;
            header.style.borderTopLeftRadius = 5;
            header.style.borderTopRightRadius = 5;
            header.style.borderBottomLeftRadius = 5;
            header.style.borderBottomRightRadius = 5;
            header.userData = new AgentSessionDropTarget(groupId == "__archive" ? string.Empty : groupId,
                groupId == "__archive", title == "PINNED", string.Empty, AgentSessionDropPlacement.After);
            header.RegisterCallback<ClickEvent>(_ =>
            {
                if (string.IsNullOrEmpty(groupId)) return;
                if (groupId == "__archive")
                {
                    _archiveCollapsed = !_archiveCollapsed;
                    _lastRevision = -1;
                    return;
                }
                RunUiTask(() => ToggleGroupCollapsedAsync(groupId));
            });
            header.RegisterCallback<PointerEnterEvent>(_ => header.style.backgroundColor = AgentUi.Hover);
            header.RegisterCallback<PointerLeaveEvent>(_ => header.style.backgroundColor = AgentUi.Transparent);
            _sessionList.Add(header);
            if (collapsed) return;
            foreach (var session in sessions) _sessionList.Add(CreateSessionItem(session));
        }

        private VisualElement CreateSessionItem(AgentSessionDocument session)
        {
            var item = new VisualElement();
            item.style.flexShrink = 0;
            item.style.minHeight = 38;
            item.style.marginBottom = 3;
            item.style.paddingLeft = 9;
            item.style.paddingRight = 7;
            item.style.paddingTop = 7;
            item.style.paddingBottom = 7;
            item.style.borderTopLeftRadius = 7;
            item.style.borderTopRightRadius = 7;
            item.style.borderBottomLeftRadius = 7;
            item.style.borderBottomRightRadius = 7;
            item.style.backgroundColor = session.Id == _selectedSessionId ? AgentUi.Selected : AgentUi.Transparent;
            AgentTooltip.Attach(item, session.Title);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexStart } };
            item.Add(row);
            var label = new Label(session.Title);
            label.style.flexGrow = 1;
            label.style.minWidth = 0;
            label.style.whiteSpace = WhiteSpace.Normal;
            row.Add(label);
            var pin = AgentUi.IconButton(session.IsPinned ? "◆" : "◇", session.IsPinned ? "Unpin" : "Pin",
                () => RunUiTask(() => UpdateOrganizationAsync(session, !session.IsPinned,
                    session.IsArchived, session.GroupId)), 24, AgentUi.Transparent);
            pin.style.fontSize = 10;
            pin.SetEnabled(!session.IsArchived);
            row.Add(pin);
            var archive = AgentUi.IconButton(session.IsArchived ? "↩" : "▾",
                session.IsArchived ? "Restore from archive" : "Archive",
                () => RunUiTask(() => UpdateOrganizationAsync(session,
                    session.IsArchived && session.IsPinned, !session.IsArchived, string.Empty)), 24,
                AgentUi.Transparent);
            archive.style.fontSize = 11;
            row.Add(archive);
            var meta = new Label(SessionMeta(session));
            meta.style.fontSize = 9;
            meta.style.color = AgentUi.Muted;
            meta.style.marginTop = 3;
            item.Add(meta);
            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != 0) return;
                _selectedSessionId = session.Id;
                _shownSessionError = string.Empty;
                _lastRevision = -1;
            });
            item.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 1) return;
                ShowSessionMenu(session, item);
                evt.StopPropagation();
            });
            item.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (session.Id != _selectedSessionId) item.style.backgroundColor = AgentUi.Hover;
            });
            item.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                item.style.backgroundColor = session.Id == _selectedSessionId
                    ? AgentUi.Selected
                    : AgentUi.Transparent;
            });
            item.userData = new AgentSessionDropTarget(
                session.IsPinned || session.IsArchived ? string.Empty : session.GroupId,
                session.IsArchived, session.IsPinned && !session.IsArchived,
                session.Id, AgentSessionDropPlacement.Before);
            item.AddManipulator(new AgentSessionDragManipulator(item, target =>
                RunUiTask(() => MoveSessionAsync(session, target))));
            return item;
        }

        private void ShowSessionMenu(AgentSessionDocument session, VisualElement anchor)
        {
            var items = new List<AgentMenuItem>
            {
                new(session.IsPinned ? "Unpin" : "Pin",
                    () => RunUiTask(() => UpdateOrganizationAsync(session, !session.IsPinned,
                        session.IsArchived, session.GroupId))),
                new(session.IsArchived ? "Restore from archive" : "Archive",
                    () => RunUiTask(() => UpdateOrganizationAsync(session,
                        session.IsArchived && session.IsPinned, !session.IsArchived, string.Empty)))
            };
            var groups = _host.Settings.ConversationGroups.OrderBy(value => value.SortOrder).ToList();
            if (groups.Count > 0)
            {
                items.Add(new AgentMenuItem("Move to · Ungrouped",
                    () => RunUiTask(() => UpdateOrganizationAsync(session, session.IsPinned,
                        session.IsArchived, "")), string.IsNullOrEmpty(session.GroupId), separatorBefore: true));
                foreach (var group in groups)
                {
                    var captured = group.Id;
                    items.Add(new AgentMenuItem("Move to · " + group.Name,
                        () => RunUiTask(() => UpdateOrganizationAsync(session, session.IsPinned,
                            session.IsArchived, captured)), session.GroupId == captured));
                }
            }
            items.Add(new AgentMenuItem("Delete conversation…", () => _showConfirmation("Delete conversation?",
                    $"Delete “{session.Title}” and its persisted transcript? This cannot be undone.",
                    () => RunUiTask(() => DeleteSessionAsync(session.Id))), dangerous: true,
                separatorBefore: true));
            AgentPopupMenu.Show(anchor, items, 230);
        }

        private async Task MoveSessionAsync(AgentSessionDocument session, AgentSessionDropTarget target)
        {
            var oldGroupId = session.IsPinned || session.IsArchived ? string.Empty : session.GroupId;
            var oldArchived = session.IsArchived;
            var oldPinned = session.IsPinned && !session.IsArchived;
            var sessions = _host.GetSessions().Where(value => value.IsArchived == target.Archived &&
                (target.Archived || value.IsPinned == target.Pinned) &&
                string.Equals(value.IsPinned || value.IsArchived ? string.Empty : value.GroupId,
                    target.GroupId, StringComparison.Ordinal) && value.Id != session.Id)
                .OrderBy(value => value.SortOrder).ThenByDescending(value => value.UpdatedAtUtc).ToList();
            var targetIndex = target.SessionId.Length == 0
                ? sessions.Count
                : sessions.FindIndex(value => value.Id == target.SessionId);
            if (targetIndex < 0) targetIndex = sessions.Count;
            if (target.Placement == AgentSessionDropPlacement.After) targetIndex++;
            sessions.Insert(Mathf.Clamp(targetIndex, 0, sessions.Count), session);
            for (var index = 0; index < sessions.Count; index++)
            {
                var value = sessions[index];
                await _host.UpdateSessionOrganizationAsync(value.Id,
                    value.Id == session.Id ? target.Pinned : value.IsPinned,
                    target.Archived, target.GroupId, index, _lifetime.Token);
            }
            if (oldArchived != target.Archived || oldPinned != target.Pinned ||
                !string.Equals(oldGroupId, target.GroupId, StringComparison.Ordinal))
            {
                var oldBucket = _host.GetSessions().Where(value => value.Id != session.Id &&
                        value.IsArchived == oldArchived && (oldArchived || value.IsPinned == oldPinned) &&
                        string.Equals(value.IsPinned || value.IsArchived ? string.Empty : value.GroupId,
                            oldGroupId, StringComparison.Ordinal))
                    .OrderBy(value => value.SortOrder).ThenByDescending(value => value.UpdatedAtUtc).ToList();
                for (var index = 0; index < oldBucket.Count; index++)
                {
                    var value = oldBucket[index];
                    await _host.UpdateSessionOrganizationAsync(value.Id, value.IsPinned, value.IsArchived,
                        value.GroupId, index, _lifetime.Token);
                }
            }
        }

        private Task UpdateOrganizationAsync(AgentSessionDocument session, bool pinned, bool archived, string groupId)
        {
            return _host.UpdateSessionOrganizationAsync(session.Id, pinned, archived, groupId,
                Math.Max(0, session.SortOrder), _lifetime.Token);
        }

        private async Task ToggleGroupCollapsedAsync(string groupId)
        {
            var settings = _host.Settings;
            var group = settings.ConversationGroups.FirstOrDefault(value => value.Id == groupId);
            if (group == null) return;
            group.IsCollapsed = !group.IsCollapsed;
            await _host.SaveSettingsAsync(settings, _lifetime.Token);
        }

        private void RefreshActionButton()
        {
            var current = CurrentSession();
            var active = current != null && IsActive(current);
            var hasText = !string.IsNullOrWhiteSpace(_composer.value);
            _action.text = hasText || !active ? "↑" : "■";
            _action.HelpText = hasText
                ? active ? "Stop the active turn and send this message" : "Send message"
                : active ? "Stop the active turn" : "Type a message to send";
            _action.SetPalette(active && !hasText ? AgentUi.Danger : AgentUi.Send,
                active && !hasText ? AgentUi.Text : AgentUi.SendForeground);
            _action.SetEnabled(active || !string.IsNullOrWhiteSpace(_composer.value));
        }

        private void ApplyChatModelOptions(string modelId)
        {
            var profile = ResolveSelectedProfile();
            if (profile == null) return;
            var efforts = AgentProviderCatalog.GetReasoningEfforts(profile.ProviderPresetId, modelId);
            var current = _effort.value;
            _effort.choices = new[] { "default" }.Concat(efforts).Distinct(StringComparer.Ordinal).ToList();
            _effort.SetValueWithoutNotify(_effort.choices.Contains(current) ? current : "default");
        }

        private void RefreshCuratedModels()
        {
            var profile = ResolveSelectedProfile();
            if (profile == null) return;
            var models = AgentProviderCatalog.GetModels(profile.ProviderPresetId).Select(value => value.Id).ToList();
            _model.SetChoices(models);
            _model.SetValueWithoutNotify(!string.IsNullOrWhiteSpace(profile.Model)
                ? profile.Model
                : models.FirstOrDefault() ?? string.Empty);
            ApplyChatModelOptions(_model.Value);
            var effort = string.IsNullOrWhiteSpace(profile.ReasoningEffort) ? "default" : profile.ReasoningEffort;
            if (!_effort.choices.Contains(effort))
            {
                var choices = _effort.choices.ToList();
                choices.Add(effort);
                _effort.choices = choices;
            }
            _effort.SetValueWithoutNotify(effort);
        }

        private AgentProviderProfile? ResolveSelectedProfile()
        {
            if (!_profileIdsByLabel.TryGetValue(_provider.value, out var profileId)) return null;
            return _host.Settings.ProviderProfiles.FirstOrDefault(value => value.Id == profileId);
        }

        private AgentSessionDocument? CurrentSession() => string.IsNullOrWhiteSpace(_selectedSessionId)
            ? null
            : _host.GetSession(_selectedSessionId);

        private static bool IsActive(AgentSessionDocument session) =>
            session.State is AgentSessionState.Running or AgentSessionState.AwaitingApproval;

        private static string SessionMeta(AgentSessionDocument session)
        {
            if (IsActive(session)) return "Running";
            var local = session.UpdatedAtUtc.ToLocalTime();
            return local.Date == DateTime.Today ? local.ToString("HH:mm") : local.ToString("MMM d");
        }

        private static VisualElement CreateEmptyState()
        {
            var empty = new VisualElement();
            empty.style.alignItems = Align.Center;
            empty.style.marginTop = 72;
            var title = new Label("What would you like to build?");
            title.style.fontSize = 21;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            empty.Add(title);
            var hint = new Label("The workspace is bound to this Unity project. Agent instructions and tools are ready.");
            hint.style.color = AgentUi.Muted;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = 7;
            empty.Add(hint);
            return empty;
        }

        private static VisualElement CreateMessage(AgentMessage message)
        {
            var box = new VisualElement();
            box.style.maxWidth = new Length(86, LengthUnit.Percent);
            box.style.alignSelf = message.Role == AgentMessageRole.User ? Align.FlexEnd : Align.FlexStart;
            box.style.flexShrink = 0;
            box.style.marginBottom = 11;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;
            box.style.paddingTop = 9;
            box.style.paddingBottom = 9;
            box.style.borderTopLeftRadius = 9;
            box.style.borderTopRightRadius = 9;
            box.style.borderBottomLeftRadius = 9;
            box.style.borderBottomRightRadius = 9;
            box.style.backgroundColor = message.Role switch
            {
                AgentMessageRole.User => AgentUi.UserMessage,
                AgentMessageRole.Tool => message.IsError ? AgentUi.ErrorPanel : AgentUi.ToolMessage,
                _ => AgentUi.AssistantMessage
            };
            var role = new Label(message.Role == AgentMessageRole.Tool
                ? "TOOL · " + message.ToolName
                : message.Role.ToString().ToUpperInvariant());
            role.style.fontSize = 10;
            role.style.unityFontStyleAndWeight = FontStyle.Bold;
            role.style.color = AgentUi.Muted;
            box.Add(role);
            if (!string.IsNullOrEmpty(message.Text))
            {
                var text = new Label(message.Text);
                text.style.whiteSpace = WhiteSpace.Normal;
                text.style.marginTop = 4;
                box.Add(text);
            }
            foreach (var call in message.ToolCalls)
            {
                var tool = new Label("↳ " + call.Name + "\n" + call.ArgumentsJson);
                tool.style.whiteSpace = WhiteSpace.Normal;
                tool.style.color = AgentUi.Muted;
                tool.style.marginTop = 6;
                box.Add(tool);
            }
            return box;
        }

        private VisualElement CreateApproval(AgentApprovalRequest approval)
        {
            var card = AgentUi.RoundedPanel(10);
            card.style.marginBottom = 12;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = AgentUi.Warning;
            card.style.backgroundColor = AgentUi.WarningPanel;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.Add(new Label("Confirmation required · " + approval.ToolName));
            var arguments = new Label(approval.ArgumentsJson);
            arguments.style.whiteSpace = WhiteSpace.Normal;
            arguments.style.marginTop = 5;
            card.Add(arguments);
            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
            actions.Add(AgentUi.Button("Approve", "Execute this non-read operation.",
                () => _host.ResolveApproval(approval.Id, true), 84, AgentUi.Accent));
            actions.Add(AgentUi.Button("Decline", "Return a declined result to the model.",
                () => _host.ResolveApproval(approval.Id, false), 84));
            card.Add(actions);
            return card;
        }

        private async void RunUiTask(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _showError("Unity Agent", exception.Message);
            }
        }

        private static string ProfileLabel(AgentProviderProfile profile) =>
            profile.Name + "  ·  " + profile.Protocol + "  ·  " + ShortId(profile.Id);

        private static string ShortId(string id) => id.Length <= 6 ? id : id.Substring(0, 6);
    }

    public sealed class AgentSettingsView : VisualElement, IDisposable
    {
        private readonly UnityAgentHost _host;
        private readonly Action _back;
        private readonly Action<string, string> _showError;
        private readonly Action<string, string, Action> _showConfirmation;
        private readonly AgentScrollContainer _scroll;
        private readonly AgentChoiceField _profiles;
        private readonly AgentChoiceField _providerPreset;
        private readonly AgentTextField _name;
        private readonly AgentChoiceField _protocol;
        private readonly AgentTextField _baseUrl;
        private readonly AgentEditableChoiceField _model;
        private readonly AgentChoiceField _effort;
        private readonly AgentIntegerField _maxTokens;
        private readonly AgentTextField _secretEnvironment;
        private readonly AgentTextField _sessionSecret;
        private readonly VisualElement _codexBlock;
        private readonly Label _codexAccount;
        private readonly AgentChoiceField _permission;
        private readonly AgentIntegerField _toolTimeout;
        private readonly AgentTextField _systemPrompt;
        private readonly AgentPathListEditor _agentsRoots;
        private readonly AgentPathListEditor _skillRoots;
        private readonly AgentPathLocationEditor _history;
        private readonly VisualElement _groups;
        private readonly Label _status;
        private readonly CancellationTokenSource _lifetime = new();
        private AgentSettingsDocument _editing = AgentSettingsDocument.CreateDefault();
        private string _selectedProfileId = string.Empty;
        private long _lastRevision = -1;
        private readonly HashSet<string> _discoveryStartedProfiles = new(StringComparer.Ordinal);
        private bool _initialized;
        private bool _disposed;

        public AgentSettingsView(
            UnityAgentHost host,
            AgentScrollContainer? scrollContainer = null,
            Action? back = null,
            Action<string, string>? showError = null,
            Action<string, string, Action>? showConfirmation = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _back = back ?? (() => { });
            _showError = showError ?? ((_, message) => Debug.LogError(message));
            _showConfirmation = showConfirmation ?? ((_, _, confirmed) => confirmed());
            style.flexGrow = 1;
            style.minWidth = 0;
            style.minHeight = 0;

            var header = new VisualElement();
            header.style.height = 58;
            header.style.flexShrink = 0;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 18;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = AgentUi.Border;
            header.Add(AgentUi.IconButton("‹", "Back to conversations", _back, 34));
            var heading = new VisualElement { style = { marginLeft = 8, flexGrow = 1 } };
            var title = new Label("Settings");
            title.style.fontSize = 19;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.Add(title);
            var subtitle = new Label("Providers, instructions, history, and conversation organization");
            subtitle.style.fontSize = 10;
            subtitle.style.color = AgentUi.Muted;
            heading.Add(subtitle);
            header.Add(heading);
            _status = new Label("Loading…");
            _status.style.color = AgentUi.Muted;
            _status.style.marginRight = 10;
            header.Add(_status);
            header.Add(AgentUi.Button("Save", "Persist all settings.", () => RunUiTask(SaveAsync), 78, AgentUi.Accent));
            Add(header);

            _scroll = scrollContainer ?? AgentScrollContainer.CreateDefault();
            _scroll.Root.style.flexGrow = 1;
            _scroll.Root.style.minHeight = 0;
            _scroll.Content.style.paddingLeft = 22;
            _scroll.Content.style.paddingRight = 22;
            _scroll.Content.style.paddingTop = 15;
            _scroll.Content.style.paddingBottom = 28;
            _scroll.Content.style.minWidth = 0;
            _scroll.Content.style.maxWidth = new Length(100, LengthUnit.Percent);
            Add(_scroll.Root);

            var providerCard = AgentUi.Card("API providers", "Select a preset, discover models, or enter a compatible endpoint.");
            _scroll.Content.Add(providerCard);
            var profileBar = AgentUi.WrapRow();
            providerCard.Add(profileBar);
            _profiles = AgentUi.Dropdown("Profile", Array.Empty<string>());
            _profiles.style.minWidth = 240;
            _profiles.style.flexGrow = 1;
            _profiles.RegisterValueChangedCallback(_ => SelectProfileByLabel(_profiles.value));
            profileBar.Add(_profiles);
            profileBar.Add(AgentUi.Button("＋ Add", "Add a provider profile.", AddProfile, 76));
            profileBar.Add(AgentUi.Button("Remove", "Remove the selected provider profile.", RemoveProfile, 78));

            _providerPreset = AgentUi.Dropdown("Provider preset",
                new[] { "Custom" }.Concat(AgentProviderCatalog.Providers.Select(PresetLabel)));
            AgentTooltip.Attach(_providerPreset,
                "Choose a built-in provider preset. Custom preserves the fields below.");
            _providerPreset.RegisterValueChangedCallback(_ => ApplyProviderPreset());
            providerCard.Add(_providerPreset);
            _name = AgentUi.Field("Display name", string.Empty, "Name shown in the chat composer.");
            providerCard.Add(_name);
            _protocol = AgentUi.Dropdown("API protocol", AgentProtocolIds.All);
            _protocol.RegisterValueChangedCallback(_ => ApplyProtocolDefaults());
            providerCard.Add(_protocol);
            _baseUrl = AgentUi.Field("Base URL", string.Empty, "API root URL, or the local Codex executable.");
            providerCard.Add(_baseUrl);
            _model = new AgentEditableChoiceField("Default model", "Discover a model or type the exact id.");
            _model.style.minWidth = 0;
            _model.ChoiceSelected += value =>
            {
                var profile = SelectedProfile();
                var preset = AgentProviderCatalog.FindProvider(profile);
                if (preset == null) return;
                ApplyModelPreset(AgentProviderCatalog.GetModel(preset.Id, value));
            };
            providerCard.Add(_model);
            _effort = AgentUi.Dropdown("Default reasoning effort",
                new[] { "default", "none", "low", "medium", "high", "xhigh" });
            providerCard.Add(_effort);
            _maxTokens = new AgentIntegerField("Max output tokens") { value = 4096 };
            providerCard.Add(_maxTokens);
            _secretEnvironment = AgentUi.Field("API key environment variable", string.Empty,
                "Portable environment variable name used to resolve the API key.");
            providerCard.Add(_secretEnvironment);
            _sessionSecret = AgentUi.Field("Session API key", string.Empty,
                "Memory-only API key. It is never written to settings or history.", true);
            providerCard.Add(_sessionSecret);
            var providerActions = AgentUi.WrapRow();
            providerActions.style.marginTop = 8;
            providerActions.Add(AgentUi.Button("Use session key", "Apply the key to this Unity process.",
                ApplySessionSecret, 122));
            providerActions.Add(AgentUi.Button("Discover models", "Fetch models; built-in defaults remain available offline.",
                () => RunUiTask(DiscoverModelsAsync), 126, AgentUi.Accent));
            providerCard.Add(providerActions);

            _codexBlock = AgentUi.Inset();
            _codexBlock.style.marginTop = 9;
            _codexBlock.Add(new Label("Codex account"));
            _codexAccount = new Label("Not checked");
            _codexAccount.style.whiteSpace = WhiteSpace.Normal;
            _codexAccount.style.color = AgentUi.Muted;
            _codexBlock.Add(_codexAccount);
            var codexActions = AgentUi.WrapRow();
            codexActions.Add(AgentUi.Button("Browser login", "Start browser authentication.",
                () => RunUiTask(() => StartCodexLoginAsync(false)), 112));
            codexActions.Add(AgentUi.Button("Device code", "Start device-code authentication.",
                () => RunUiTask(() => StartCodexLoginAsync(true)), 104));
            codexActions.Add(AgentUi.Button("Refresh", "Refresh account state.",
                () => RunUiTask(RefreshCodexAccountAsync), 82));
            _codexBlock.Add(codexActions);
            providerCard.Add(_codexBlock);

            var defaults = AgentUi.Card("Agent defaults", "Applied to new conversations. The workspace is always this Unity project.");
            _scroll.Content.Add(defaults);
            _permission = AgentUi.Dropdown("Default permission", new[]
            {
                AgentPermissionMode.FullAccess.ToString(), AgentPermissionMode.ConfirmWrites.ToString()
            });
            defaults.Add(_permission);
            _toolTimeout = new AgentIntegerField("Default tool timeout (seconds)") { value = 120 };
            AgentTooltip.Attach(_toolTimeout,
                "Used when a process, shell, or Unity eval tool call does not specify its own timeout.");
            defaults.Add(_toolTimeout);
            _systemPrompt = AgentUi.Field("System prompt", string.Empty,
                "Global Agent instructions. This is intentionally unavailable inside a conversation.");
            _systemPrompt.multiline = true;
            _systemPrompt.style.minHeight = 150;
            _systemPrompt.style.whiteSpace = WhiteSpace.Normal;
            defaults.Add(_systemPrompt);

            var agentsCard = AgentUi.Card("AGENTS.md discovery", "Ordered highest priority first. Each root is portable across computers.");
            _scroll.Content.Add(agentsCard);
            _agentsRoots = new AgentPathListEditor("AGENTS.md roots", "Add AGENTS.md root", ShowPathError);
            agentsCard.Add(_agentsRoots);

            var skillsCard = AgentUi.Card("Skills discovery", "Configured separately from AGENTS.md. Point directly to a directory containing Skills.");
            _scroll.Content.Add(skillsCard);
            _skillRoots = new AgentPathListEditor("Skill roots", "Add Skill root", ShowPathError);
            skillsCard.Add(_skillRoots);

            var historyCard = AgentUi.Card("Conversation history", "Current persisted transcript location. Changing it migrates existing history.");
            _scroll.Content.Add(historyCard);
            _history = new AgentPathLocationEditor(false, ShowPathError);
            historyCard.Add(_history);

            var groupsCard = AgentUi.Card("Conversation groups", "Groups appear in the chat sidebar. Conversations can be dragged onto a group.");
            _scroll.Content.Add(groupsCard);
            _groups = new VisualElement();
            groupsCard.Add(_groups);
            groupsCard.Add(AgentUi.Button("＋ New group", "Create a conversation group.", AddGroup, 110));

            var fileCard = AgentUi.Card("Settings file", "The same JSON file can be edited outside Unity and reloaded here.");
            _scroll.Content.Add(fileCard);
            var settingsPath = Path.Combine(AgentPaths.SettingsRoot, AgentPaths.SettingsFileName);
            var path = new Label(settingsPath);
            path.style.whiteSpace = WhiteSpace.Normal;
            path.style.color = AgentUi.Muted;
            AgentTooltip.Attach(path, settingsPath);
            fileCard.Add(path);
            fileCard.Add(AgentUi.Button("Reload from disk", "Discard unsaved UI edits and reload settings.json.",
                () => _showConfirmation("Reload settings?", "Discard unsaved changes in this page and reload settings.json?",
                    () => RunUiTask(ReloadAsync)), 128));

            RunUiTask(InitializeAsync);
        }

        public void Tick()
        {
            if (_disposed || !_initialized) return;
            // Settings edits are deliberately not overwritten by background Host revisions.
            _lastRevision = _host.Revision;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async Task InitializeAsync()
        {
            await _host.EnsureInitializedAsync(_lifetime.Token);
            _editing = _host.Settings;
            _selectedProfileId = _editing.DefaultProviderProfileId;
            RefreshProfileChoices();
            LoadSelectedProfile();
            _permission.SetValueWithoutNotify(_editing.PermissionMode.ToString());
            _toolTimeout.SetValueWithoutNotify(_editing.DefaultToolTimeoutSeconds);
            _systemPrompt.SetValueWithoutNotify(_editing.SystemPrompt);
            _agentsRoots.SetItems(_editing.AgentsRoots);
            _skillRoots.SetItems(_editing.SkillRoots);
            _history.SetValue(_editing.HistoryLocation);
            RefreshGroups();
            _initialized = true;
            _lastRevision = _host.Revision;
            _status.text = "Ready";
            try
            {
                await DiscoverSettingsModelsOnceAsync(_selectedProfileId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _showError("Model discovery failed", exception.Message +
                    "\n\nCurated models remain available and you can type an exact model id.");
            }
        }

        private async Task SaveAsync()
        {
            SaveSelectedProfileFields();
            _editing.PermissionMode = Enum.TryParse<AgentPermissionMode>(_permission.value, out var permission)
                ? permission
                : AgentPermissionMode.FullAccess;
            _editing.DefaultToolTimeoutSeconds = Math.Max(1, _toolTimeout.value);
            _editing.SystemPrompt = string.IsNullOrWhiteSpace(_systemPrompt.value)
                ? AgentPromptDefaults.SystemPrompt
                : _systemPrompt.value;
            _editing.AgentsRoots = _agentsRoots.GetItems();
            _editing.SkillRoots = _skillRoots.GetItems();
            _editing.HistoryLocation = _history.GetValue();
            _editing.DefaultProviderProfileId = _selectedProfileId;
            await _host.SaveSettingsAsync(_editing, _lifetime.Token);
            _editing = _host.Settings;
            _status.text = "Saved  ·  " + DateTime.Now.ToString("HH:mm:ss");
        }

        private async Task DiscoverModelsAsync()
        {
            SaveSelectedProfileFields();
            var result = await _host.DiscoverModelsAsync(SelectedProfile(), _lifetime.Token);
            _model.SetChoices(result.Models.Select(value => value.Id));
            if (string.IsNullOrWhiteSpace(_model.Value) && result.Models.Count > 0)
                ApplyModelOption(result.Models[0]);
            else
                ApplyModelOption(result.Models.FirstOrDefault(value => value.Id == _model.Value));
            _status.text = result.Models.Count == 0
                ? "No models returned — enter an exact id"
                : $"{result.Models.Count} models · {result.Source}";
            if (!string.IsNullOrWhiteSpace(result.Warning))
                _showError("Using curated model defaults", result.Warning);
        }

        private Task DiscoverSettingsModelsOnceAsync(string profileId)
        {
            if (!_discoveryStartedProfiles.Add(profileId)) return Task.CompletedTask;
            return DiscoverSettingsModelsForProfileAsync(profileId);
        }

        private async Task DiscoverSettingsModelsForProfileAsync(string profileId)
        {
            var profile = _editing.ProviderProfiles.FirstOrDefault(value => value.Id == profileId);
            if (profile == null) return;
            var result = await _host.DiscoverModelsAsync(profile, _lifetime.Token);
            if (_selectedProfileId != profileId) return;
            _model.SetChoices(result.Models.Select(value => value.Id));
            if (string.IsNullOrWhiteSpace(_model.Value) && result.Models.Count > 0)
                ApplyModelOption(result.Models[0]);
            else
                ApplyModelOption(result.Models.FirstOrDefault(value => value.Id == _model.Value));
            _status.text = result.Models.Count == 0
                ? "No models returned — enter an exact id"
                : $"{result.Models.Count} models · {result.Source}";
            AgentTooltip.Attach(_status, result.Warning);
        }

        private async Task ReloadAsync()
        {
            await _host.ReloadSettingsFromDiskAsync(_lifetime.Token);
            _editing = _host.Settings;
            _selectedProfileId = _editing.DefaultProviderProfileId;
            RefreshProfileChoices();
            LoadSelectedProfile();
            _permission.SetValueWithoutNotify(_editing.PermissionMode.ToString());
            _toolTimeout.SetValueWithoutNotify(_editing.DefaultToolTimeoutSeconds);
            _systemPrompt.SetValueWithoutNotify(_editing.SystemPrompt);
            _agentsRoots.SetItems(_editing.AgentsRoots);
            _skillRoots.SetItems(_editing.SkillRoots);
            _history.SetValue(_editing.HistoryLocation);
            RefreshGroups();
            _status.text = "Reloaded from disk";
        }

        private void AddProfile()
        {
            SaveSelectedProfileFields();
            var profile = new AgentProviderProfile { Name = "New provider", ProviderPresetId = "custom" };
            _editing.ProviderProfiles.Add(profile);
            _selectedProfileId = profile.Id;
            RefreshProfileChoices();
            LoadSelectedProfile();
            RunUiTask(() => DiscoverSettingsModelsOnceAsync(profile.Id));
        }

        private void RemoveProfile()
        {
            if (_editing.ProviderProfiles.Count <= 1)
            {
                _showError("Provider required", "At least one provider profile is required.");
                return;
            }
            var profile = SelectedProfile();
            _showConfirmation("Remove provider?", $"Remove provider profile “{profile.Name}”?", () =>
            {
                _editing.ProviderProfiles.RemoveAll(value => value.Id == _selectedProfileId);
                _selectedProfileId = _editing.ProviderProfiles[0].Id;
                RefreshProfileChoices();
                LoadSelectedProfile();
            });
        }

        private void SelectProfileByLabel(string label)
        {
            SaveSelectedProfileFields();
            var profile = _editing.ProviderProfiles.FirstOrDefault(value => ProfileLabel(value) == label);
            if (profile == null) return;
            _selectedProfileId = profile.Id;
            LoadSelectedProfile();
            RunUiTask(() => DiscoverSettingsModelsOnceAsync(profile.Id));
        }

        private void SaveSelectedProfileFields()
        {
            var profile = _editing.ProviderProfiles.FirstOrDefault(value => value.Id == _selectedProfileId);
            if (profile == null) return;
            profile.Name = string.IsNullOrWhiteSpace(_name.value) ? "Provider" : _name.value.Trim();
            var selectedPreset = AgentProviderCatalog.Providers.FirstOrDefault(value =>
                PresetLabel(value) == _providerPreset.value);
            profile.ProviderPresetId = selectedPreset?.Id ?? "custom";
            profile.Protocol = _protocol.value;
            profile.BaseUrl = profile.Protocol == AgentProtocolIds.CodexAppServer && string.IsNullOrWhiteSpace(_baseUrl.value)
                ? "codex"
                : _baseUrl.value.Trim();
            profile.Model = _model.Value.Trim();
            profile.ReasoningEffort = _effort.value == "default" ? string.Empty : _effort.value;
            profile.MaxOutputTokens = Math.Max(1, _maxTokens.value);
            profile.SecretEnvironmentVariable = profile.Protocol == AgentProtocolIds.CodexAppServer
                ? string.Empty
                : _secretEnvironment.value.Trim();
            RefreshProfileChoices(false);
        }

        private void LoadSelectedProfile()
        {
            var profile = SelectedProfile();
            _profiles.SetValueWithoutNotify(ProfileLabel(profile));
            var preset = AgentProviderCatalog.FindProvider(profile.ProviderPresetId);
            _providerPreset.SetValueWithoutNotify(preset == null ? "Custom" : PresetLabel(preset));
            _name.SetValueWithoutNotify(profile.Name);
            _protocol.SetValueWithoutNotify(profile.Protocol);
            _baseUrl.SetValueWithoutNotify(profile.BaseUrl);
            _model.SetValueWithoutNotify(profile.Model);
            _model.SetChoices(AgentProviderCatalog.GetModels(profile.ProviderPresetId).Select(value => value.Id));
            _effort.SetValueWithoutNotify(string.IsNullOrWhiteSpace(profile.ReasoningEffort)
                ? "default"
                : profile.ReasoningEffort);
            EnsureChoice(_effort, _effort.value);
            _maxTokens.SetValueWithoutNotify(profile.MaxOutputTokens);
            _secretEnvironment.SetValueWithoutNotify(profile.SecretEnvironmentVariable);
            _sessionSecret.SetValueWithoutNotify(string.Empty);
            UpdateProtocolPresentation();
            if (profile.Protocol == AgentProtocolIds.CodexAppServer) RunUiTask(RefreshCodexAccountAsync);
        }

        private void ApplyProviderPreset()
        {
            if (!_initialized) return;
            if (_providerPreset.value == "Custom") return;
            var preset = AgentProviderCatalog.Providers.FirstOrDefault(value => PresetLabel(value) == _providerPreset.value);
            if (preset == null) return;
            var profile = SelectedProfile();
            AgentProviderCatalog.ApplyPreset(profile, preset.Id);
            LoadSelectedProfile();
            _status.text = preset.DisplayName + " defaults applied";
        }

        private void ApplyModelOption(AgentModelOption? option)
        {
            if (option == null) return;
            _model.SetValue(option.Id);
            var efforts = option.ReasoningEfforts.Count == 0
                ? new[] { "default" }
                : new[] { "default" }.Concat(option.ReasoningEfforts).Distinct(StringComparer.Ordinal).ToArray();
            _effort.choices = efforts.ToList();
            _effort.SetValueWithoutNotify(string.IsNullOrWhiteSpace(option.DefaultReasoningEffort)
                ? "default"
                : option.DefaultReasoningEffort);
            if (option.RecommendedOutputTokens > 0) _maxTokens.value = option.RecommendedOutputTokens;
        }

        private void ApplyModelPreset(AgentModelPreset? model)
        {
            if (model == null) return;
            var efforts = model.ReasoningEfforts.Count == 0
                ? new[] { "default" }
                : new[] { "default" }.Concat(model.ReasoningEfforts).Distinct(StringComparer.Ordinal).ToArray();
            _effort.choices = efforts.ToList();
            _effort.SetValueWithoutNotify(string.IsNullOrWhiteSpace(model.DefaultReasoningEffort)
                ? "default"
                : model.DefaultReasoningEffort);
            if (model.RecommendedOutputTokens > 0) _maxTokens.value = model.RecommendedOutputTokens;
        }

        private void ApplyProtocolDefaults()
        {
            if (_protocol.value == AgentProtocolIds.CodexAppServer)
            {
                if (string.IsNullOrWhiteSpace(_baseUrl.value) || LooksLikeHttpEndpoint(_baseUrl.value))
                    _baseUrl.value = "codex";
                _secretEnvironment.value = string.Empty;
                _sessionSecret.value = string.Empty;
            }
            else if (_protocol.value == AgentProtocolIds.AnthropicMessages)
            {
                if (string.IsNullOrWhiteSpace(_baseUrl.value) || _baseUrl.value == "codex")
                    _baseUrl.value = "https://api.anthropic.com/v1/";
                if (string.IsNullOrWhiteSpace(_secretEnvironment.value))
                    _secretEnvironment.value = "ANTHROPIC_API_KEY";
            }
            else if (string.IsNullOrWhiteSpace(_baseUrl.value) || _baseUrl.value == "codex")
            {
                _baseUrl.value = "https://api.openai.com/v1/";
                if (string.IsNullOrWhiteSpace(_secretEnvironment.value))
                    _secretEnvironment.value = "OPENAI_API_KEY";
            }
            UpdateProtocolPresentation();
        }

        private void ApplySessionSecret()
        {
            if (_protocol.value == AgentProtocolIds.CodexAppServer)
            {
                _showError("Codex authentication", "Codex uses its local account login and does not accept an API key here.");
                return;
            }
            _host.Secrets.SetSessionSecret(SelectedProfile().Id, _sessionSecret.value);
            _sessionSecret.value = string.Empty;
            _status.text = "Session key applied in memory";
        }

        private void UpdateProtocolPresentation()
        {
            var codex = _protocol.value == AgentProtocolIds.CodexAppServer;
            _baseUrl.label = codex ? "Codex executable" : "Base URL";
            _secretEnvironment.SetEnabled(!codex);
            _sessionSecret.SetEnabled(!codex);
            _codexBlock.style.display = codex ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private async Task RefreshCodexAccountAsync()
        {
            SaveSelectedProfileFields();
            var account = await _host.GetCodexAccountAsync(SelectedProfile(), _lifetime.Token);
            _codexAccount.text = account.IsSignedIn
                ? "Signed in" + Part(account.Email) + Part(account.PlanType) + Part(account.AccountType)
                : account.RequiresOpenAiAuth ? "Not signed in · ChatGPT authentication required" : "Not signed in";
        }

        private async Task StartCodexLoginAsync(bool deviceCode)
        {
            SaveSelectedProfileFields();
            var login = await _host.StartCodexLoginAsync(SelectedProfile(), deviceCode, _lifetime.Token);
            if (string.IsNullOrWhiteSpace(login.AuthorizationUrl))
                throw new InvalidOperationException("Codex did not return an authorization URL.");
            Application.OpenURL(login.AuthorizationUrl);
            _codexAccount.text = string.IsNullOrWhiteSpace(login.UserCode)
                ? "Complete authentication in the opened browser, then Refresh."
                : "Enter code " + login.UserCode + " in the opened page, then Refresh.";
        }

        private void AddGroup()
        {
            _editing.ConversationGroups.Add(new AgentConversationGroup
            {
                Name = "New group",
                SortOrder = _editing.ConversationGroups.Count
            });
            RefreshGroups();
        }

        private void RefreshGroups()
        {
            _groups.Clear();
            foreach (var group in _editing.ConversationGroups.OrderBy(value => value.SortOrder).ToList())
            {
                var row = AgentUi.WrapRow();
                row.style.marginBottom = 6;
                var name = AgentUi.Field(string.Empty, group.Name, "Group name");
                name.style.flexGrow = 1;
                name.RegisterValueChangedCallback(evt => group.Name = evt.newValue);
                row.Add(name);
                var collapsed = new AgentToggle("Collapsed") { value = group.IsCollapsed };
                collapsed.RegisterValueChangedCallback(evt => group.IsCollapsed = evt.newValue);
                row.Add(collapsed);
                row.Add(AgentUi.IconButton("↑", "Move group up", () => MoveGroup(group, -1), 30));
                row.Add(AgentUi.IconButton("↓", "Move group down", () => MoveGroup(group, 1), 30));
                row.Add(AgentUi.IconButton("×", "Delete group", () => RemoveGroup(group), 30, AgentUi.Danger));
                _groups.Add(row);
            }
            if (_editing.ConversationGroups.Count == 0)
            {
                var empty = new Label("No groups. Conversations remain in the ungrouped section.");
                empty.style.color = AgentUi.Muted;
                empty.style.marginBottom = 8;
                _groups.Add(empty);
            }
        }

        private void MoveGroup(AgentConversationGroup group, int delta)
        {
            var list = _editing.ConversationGroups.OrderBy(value => value.SortOrder).ToList();
            var index = list.IndexOf(group);
            var target = Mathf.Clamp(index + delta, 0, list.Count - 1);
            if (target == index) return;
            list.RemoveAt(index);
            list.Insert(target, group);
            for (var i = 0; i < list.Count; i++) list[i].SortOrder = i;
            _editing.ConversationGroups = list;
            RefreshGroups();
        }

        private void RemoveGroup(AgentConversationGroup group)
        {
            _showConfirmation("Delete group?", $"Delete group “{group.Name}”? Conversations will be moved to Ungrouped.",
                () => RunUiTask(() => RemoveGroupAsync(group)));
        }

        private async Task RemoveGroupAsync(AgentConversationGroup group)
        {
            var previous = _host.Settings;
            var changed = _host.Settings;
            var members = _host.GetSessions().Where(value => value.GroupId == group.Id)
                .Select(value => new AgentSessionOrganizationSnapshot(value)).ToList();
            changed.ConversationGroups.RemoveAll(value => value.Id == group.Id);
            try
            {
                await _host.SaveSettingsAsync(changed, _lifetime.Token);
                _editing = _host.Settings;
                RefreshGroups();
            }
            catch
            {
                try
                {
                    await _host.SaveSettingsAsync(previous, CancellationToken.None);
                    foreach (var member in members)
                        await _host.UpdateSessionOrganizationAsync(member.Id, member.IsPinned, member.IsArchived,
                            member.GroupId, member.SortOrder, CancellationToken.None);
                }
                catch
                {
                    // Preserve and rethrow the original failure; Host persistence is authoritative.
                }
                throw;
            }
        }

        private void ShowPathError(string message) => _showError("Invalid path", message);

        private AgentProviderProfile SelectedProfile() =>
            _editing.ProviderProfiles.First(value => value.Id == _selectedProfileId);

        private void RefreshProfileChoices(bool update = true)
        {
            _profiles.choices = _editing.ProviderProfiles.Select(ProfileLabel).ToList();
            if (update) _profiles.SetValueWithoutNotify(ProfileLabel(SelectedProfile()));
        }

        private async void RunUiTask(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _showError("Unity Agent settings", exception.Message);
            }
        }

        private static void EnsureChoice(AgentChoiceField field, string value)
        {
            if (field.choices.Contains(value)) return;
            var choices = field.choices.ToList();
            choices.Add(value);
            field.choices = choices;
        }

        private static bool LooksLikeHttpEndpoint(string value) =>
            value.TrimStart().StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        private static string Part(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : " · " + value;
        private static string PresetLabel(AgentProviderPreset preset) => preset.DisplayName + "  ·  " + preset.Id;
        private static string ProfileLabel(AgentProviderProfile profile) =>
            profile.Name + "  ·  " + profile.Protocol + "  ·  " +
            (profile.Id.Length <= 6 ? profile.Id : profile.Id.Substring(0, 6));
    }

    internal sealed class AgentPathListEditor : VisualElement
    {
        private readonly string _addLabel;
        private readonly Action<string> _showError;
        private readonly VisualElement _list;
        private readonly List<AgentPathLocation> _items = new();
        private readonly List<AgentPathLocationEditor> _editors = new();

        public AgentPathListEditor(string label, string addLabel, Action<string> showError)
        {
            _addLabel = addLabel;
            _showError = showError;
            var heading = new Label(label);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = 5;
            Add(heading);
            _list = new VisualElement();
            Add(_list);
            Add(AgentUi.Button("＋ " + _addLabel, "Add a lower-priority root.", AddItem, 150));
        }

        public void SetItems(IEnumerable<AgentPathLocation> items)
        {
            _items.Clear();
            _items.AddRange(items.Select(Clone));
            Refresh();
        }

        public List<AgentPathLocation> GetItems()
        {
            var drafts = _editors.Select(value => value.GetValue()).ToList();
            var duplicate = drafts.GroupBy(value => value.Id, StringComparer.Ordinal)
                .FirstOrDefault(value => value.Count() > 1);
            if (duplicate != null) throw new InvalidOperationException("Path root ids must be unique.");
            return drafts;
        }

        private void AddItem()
        {
            _items.Add(new AgentPathLocation
            {
                BasePath = AgentPathBase.ProjectRoot,
                RelativePath = string.Empty,
                IncludeInPlayerBuild = false
            });
            Refresh();
        }

        private void Refresh()
        {
            _list.Clear();
            _editors.Clear();
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var index = i;
                var card = AgentUi.Inset();
                card.style.marginBottom = 7;
                var top = AgentUi.WrapRow();
                var priority = new Label("Priority " + (i + 1));
                priority.style.flexGrow = 1;
                priority.style.unityFontStyleAndWeight = FontStyle.Bold;
                top.Add(priority);
                top.Add(AgentUi.IconButton("↑", "Raise priority", () => Move(index, -1), 30));
                top.Add(AgentUi.IconButton("↓", "Lower priority", () => Move(index, 1), 30));
                top.Add(AgentUi.IconButton("×", "Remove root", () => Remove(index), 30, AgentUi.Danger));
                card.Add(top);
                var editor = new AgentPathLocationEditor(true, _showError);
                editor.SetValue(item);
                _editors.Add(editor);
                editor.Changed += value =>
                {
                    value.Id = item.Id;
                    _items[index] = value;
                };
                card.Add(editor);
                _list.Add(card);
            }
            if (_items.Count == 0)
            {
                var empty = new Label("No roots configured. Add one to enable discovery.");
                empty.style.color = AgentUi.Muted;
                empty.style.marginBottom = 8;
                _list.Add(empty);
            }
        }

        private void Move(int index, int delta)
        {
            var target = Mathf.Clamp(index + delta, 0, _items.Count - 1);
            if (target == index) return;
            var item = _items[index];
            _items.RemoveAt(index);
            _items.Insert(target, item);
            Refresh();
        }

        private void Remove(int index)
        {
            _items.RemoveAt(index);
            Refresh();
        }

        private static AgentPathLocation Clone(AgentPathLocation value) => new()
        {
            Id = value.Id,
            BasePath = value.BasePath,
            RelativePath = value.RelativePath,
            IncludeInPlayerBuild = value.IncludeInPlayerBuild
        };
    }

    internal sealed class AgentPathLocationEditor : VisualElement
    {
        private readonly AgentChoiceField _basePath;
        private readonly AgentTextField _relativePath;
        private readonly AgentToggle? _includeInBuild;
        private readonly Label _preview;
        private readonly Action<string> _showError;
        private string _id = Guid.NewGuid().ToString("N");

        public AgentPathLocationEditor(bool showBuildToggle, Action<string> showError)
        {
            _showError = showError;
            var row = AgentUi.WrapRow();
            Add(row);
            _basePath = AgentUi.Dropdown("Base", Enum.GetNames(typeof(AgentPathBase)));
            // Keep stable path anchors readable (for example PersistentData and
            // RoamingApplicationData). The wrapping row handles narrow windows, so shrinking
            // this field only makes the portable-path choice unnecessarily ambiguous.
            _basePath.style.width = 280;
            _basePath.style.flexShrink = 0;
            _basePath.RegisterValueChangedCallback(_ => ChangedByUser());
            row.Add(_basePath);
            _relativePath = AgentUi.Field("Relative path", string.Empty,
                "Optional path relative to the selected stable base. Absolute paths are rejected.");
            _relativePath.style.flexGrow = 1;
            _relativePath.style.minWidth = 0;
            _relativePath.RegisterValueChangedCallback(_ => ChangedByUser());
            row.Add(_relativePath);
            if (showBuildToggle)
            {
                _includeInBuild = new AgentToggle("Player build");
                AgentTooltip.Attach(_includeInBuild,
                    "Package this root into Player content. Review external files before enabling.");
                _includeInBuild.RegisterValueChangedCallback(_ => ChangedByUser());
                row.Add(_includeInBuild);
            }
            _preview = new Label();
            _preview.style.fontSize = 10;
            _preview.style.color = AgentUi.Muted;
            _preview.style.whiteSpace = WhiteSpace.Normal;
            _preview.style.marginTop = 3;
            Add(_preview);
        }

        public event Action<AgentPathLocation>? Changed;

        public void SetValue(AgentPathLocation value)
        {
            _id = value.Id;
            _basePath.SetValueWithoutNotify(value.BasePath.ToString());
            _relativePath.SetValueWithoutNotify(value.RelativePath);
            _includeInBuild?.SetValueWithoutNotify(value.IncludeInPlayerBuild);
            RefreshPreview();
        }

        public AgentPathLocation GetValue()
        {
            var value = CreateValue();
            AgentPaths.Validate(value);
            return value;
        }

        private AgentPathLocation CreateValue()
        {
            var basePath = Enum.TryParse<AgentPathBase>(_basePath.value, out var parsed)
                ? parsed
                : AgentPathBase.ProjectRoot;
            return new AgentPathLocation
            {
                Id = _id,
                BasePath = basePath,
                RelativePath = _relativePath.value.Trim(),
                IncludeInPlayerBuild = _includeInBuild?.value ?? false
            };
        }

        private void ChangedByUser()
        {
            try
            {
                var value = GetValue();
                RefreshPreview();
                Changed?.Invoke(value);
            }
            catch (Exception exception)
            {
                _preview.text = "Invalid path";
                _preview.style.color = AgentUi.Error;
                _showError(exception.Message);
            }
        }

        private void RefreshPreview()
        {
            try
            {
                _preview.text = AgentPaths.Resolve(CreateValue());
                _preview.style.color = AgentUi.Muted;
            }
            catch (Exception exception)
            {
                _preview.text = exception.Message;
                _preview.style.color = AgentUi.Error;
            }
        }
    }

    internal sealed class AgentEditableChoiceField : VisualElement
    {
        private readonly AgentTextField _field;
        private readonly AgentButton _menuButton;
        private readonly List<string> _choices = new();

        public AgentEditableChoiceField(string label, string tooltip)
        {
            style.minWidth = 0;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.FlexEnd;
            style.marginTop = 4;
            style.marginBottom = 4;
            _field = string.IsNullOrEmpty(label)
                ? new AgentTextField(surface: false) { value = string.Empty }
                : AgentUi.Field(label, string.Empty, tooltip);
            if (string.IsNullOrEmpty(label)) AgentTooltip.Attach(_field, tooltip);
            _field.style.flexGrow = 1;
            _field.style.minWidth = 0;
            _field.style.flexShrink = 1;
            _field.style.marginTop = 0;
            _field.style.marginBottom = 0;
            _field.RegisterCallback<FocusOutEvent>(_ => ValueCommitted?.Invoke());
            Add(_field);
            _menuButton = AgentUi.IconButton("⌄", "Choose a discovered or built-in model", ShowMenu, 32);
            _menuButton.style.marginBottom = 1;
            Add(_menuButton);
        }

        public string Value => _field.value ?? string.Empty;
        public event Action? ValueCommitted;
        public event Action<string>? ChoiceSelected;
        public void SetValue(string value) => _field.value = value ?? string.Empty;
        public void SetValueWithoutNotify(string value) => _field.SetValueWithoutNotify(value ?? string.Empty);

        public void SetChoices(IEnumerable<string> choices)
        {
            _choices.Clear();
            _choices.AddRange(choices.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal));
        }

        public new void SetEnabled(bool value)
        {
            base.SetEnabled(value);
            _field.SetEnabled(value);
            _menuButton.SetEnabled(value);
            style.opacity = value ? 1f : 0.42f;
        }

        private void ShowMenu()
        {
            var items = new List<AgentMenuItem>();
            if (_choices.Count == 0)
                items.Add(new AgentMenuItem("No discovered models — type an id", null,
                    disabled: true));
            foreach (var choice in _choices)
            {
                var captured = choice;
                items.Add(new AgentMenuItem(captured, () =>
                    {
                        SetValue(captured);
                        ChoiceSelected?.Invoke(captured);
                        ValueCommitted?.Invoke();
                    }, string.Equals(Value, captured, StringComparison.Ordinal)));
            }
            AgentPopupMenu.Show(_menuButton, items, Math.Max(220, Mathf.RoundToInt(worldBound.width)));
        }
    }

    internal sealed class AgentModalLayer : VisualElement
    {
        private readonly Label _title;
        private readonly Label _message;
        private readonly AgentButton _cancel;
        private readonly AgentButton _confirm;
        private Action? _confirmed;

        public AgentModalLayer()
        {
            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.top = 0;
            style.bottom = 0;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.68f);
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.display = DisplayStyle.None;

            var dialog = AgentUi.RoundedPanel(12);
            dialog.style.width = new Length(78, LengthUnit.Percent);
            dialog.style.maxWidth = 540;
            dialog.style.minWidth = 280;
            dialog.style.paddingLeft = 18;
            dialog.style.paddingRight = 18;
            dialog.style.paddingTop = 16;
            dialog.style.paddingBottom = 14;
            dialog.style.borderTopWidth = 1;
            dialog.style.borderBottomWidth = 1;
            dialog.style.borderLeftWidth = 1;
            dialog.style.borderRightWidth = 1;
            dialog.style.borderTopColor = AgentUi.BorderStrong;
            dialog.style.borderBottomColor = AgentUi.BorderStrong;
            dialog.style.borderLeftColor = AgentUi.BorderStrong;
            dialog.style.borderRightColor = AgentUi.BorderStrong;
            Add(dialog);
            _title = new Label();
            _title.style.fontSize = 17;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            dialog.Add(_title);
            _message = new Label();
            _message.style.whiteSpace = WhiteSpace.Normal;
            _message.style.marginTop = 9;
            _message.style.marginBottom = 14;
            dialog.Add(_message);
            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            dialog.Add(buttons);
            _cancel = AgentUi.Button("Cancel", "Close this dialog.", Hide, 78);
            buttons.Add(_cancel);
            _confirm = AgentUi.Button("OK", "Confirm.", Confirm, 78, AgentUi.Accent);
            buttons.Add(_confirm);
        }

        public void ShowError(string title, string message)
        {
            _title.text = title;
            _message.text = message;
            _confirmed = null;
            _cancel.style.display = DisplayStyle.None;
            _confirm.text = "Close";
            _confirm.HelpText = "Close this dialog.";
            style.display = DisplayStyle.Flex;
            BringToFront();
        }

        public void ShowConfirmation(string title, string message, Action confirmed)
        {
            _title.text = title;
            _message.text = message;
            _confirmed = confirmed;
            _cancel.style.display = DisplayStyle.Flex;
            _confirm.text = "Confirm";
            _confirm.HelpText = "Confirm this action.";
            style.display = DisplayStyle.Flex;
            BringToFront();
        }

        private void Confirm()
        {
            var callback = _confirmed;
            Hide();
            callback?.Invoke();
        }

        private void Hide()
        {
            _confirmed = null;
            style.display = DisplayStyle.None;
        }
    }

    internal sealed class AgentSessionDropTarget
    {
        public AgentSessionDropTarget(
            string groupId,
            bool archived,
            bool pinned,
            string sessionId,
            AgentSessionDropPlacement placement)
        {
            GroupId = groupId;
            Archived = archived;
            Pinned = pinned;
            SessionId = sessionId;
            Placement = placement;
        }

        public string GroupId { get; }
        public bool Archived { get; }
        public bool Pinned { get; }
        public string SessionId { get; }
        public AgentSessionDropPlacement Placement { get; }
    }

    internal sealed class AgentSessionOrganizationSnapshot
    {
        public AgentSessionOrganizationSnapshot(AgentSessionDocument session)
        {
            Id = session.Id;
            IsPinned = session.IsPinned;
            IsArchived = session.IsArchived;
            GroupId = session.GroupId;
            SortOrder = Math.Max(0, session.SortOrder);
        }

        public string Id { get; }
        public bool IsPinned { get; }
        public bool IsArchived { get; }
        public string GroupId { get; }
        public int SortOrder { get; }
    }

    internal enum AgentSessionDropPlacement
    {
        Before,
        After
    }

    internal sealed class AgentSessionDragManipulator : PointerManipulator
    {
        private readonly Action<AgentSessionDropTarget> _dropped;
        private bool _active;
        private bool _dragging;
        private int _pointerId;
        private Vector2 _start;

        public AgentSessionDragManipulator(VisualElement targetElement, Action<AgentSessionDropTarget> dropped)
        {
            target = targetElement;
            _dropped = dropped;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(Down, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(Move);
            target.RegisterCallback<PointerUpEvent>(Up);
            target.RegisterCallback<PointerCancelEvent>(Cancel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(Down, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(Move);
            target.UnregisterCallback<PointerUpEvent>(Up);
            target.UnregisterCallback<PointerCancelEvent>(Cancel);
        }

        private void Down(PointerDownEvent evt)
        {
            if (evt.button != 0 || IsInteractive(evt.target as VisualElement)) return;
            _active = true;
            _dragging = false;
            _pointerId = evt.pointerId;
            _start = evt.position;
            target.CapturePointer(evt.pointerId);
        }

        private bool IsInteractive(VisualElement? element)
        {
            while (element != null && element != target)
            {
                if (element is AgentButton || element is AgentTextField || element is AgentChoiceField ||
                    element is AgentToggle || element is AgentIntegerField || element is AgentEditableChoiceField)
                    return true;
                element = element.parent;
            }
            return false;
        }

        private void Move(PointerMoveEvent evt)
        {
            if (!_active || evt.pointerId != _pointerId) return;
            if (!_dragging && ((Vector2)evt.position - _start).sqrMagnitude < 25f) return;
            _dragging = true;
            target.style.opacity = 0.55f;
        }

        private void Up(PointerUpEvent evt)
        {
            if (!_active || evt.pointerId != _pointerId) return;
            if (_dragging && target.panel != null)
            {
                var picked = target.panel.Pick(evt.position);
                while (picked != null)
                {
                    if (picked.userData is AgentSessionDropTarget drop)
                    {
                        if (drop.SessionId.Length > 0)
                        {
                            var placement = evt.position.y > picked.worldBound.center.y
                                ? AgentSessionDropPlacement.After
                                : AgentSessionDropPlacement.Before;
                            _dropped(new AgentSessionDropTarget(drop.GroupId, drop.Archived, drop.Pinned,
                                drop.SessionId, placement));
                        }
                        else
                        {
                            _dropped(drop);
                        }
                        break;
                    }
                    picked = picked.parent;
                }
            }
            Finish();
        }

        private void Cancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == _pointerId) Finish();
        }

        private void Finish()
        {
            if (target.HasPointerCapture(_pointerId)) target.ReleasePointer(_pointerId);
            target.style.opacity = 1f;
            _active = false;
            _dragging = false;
        }
    }

    internal static class AgentUi
    {
        public static readonly Color Background = new(0.047f, 0.055f, 0.063f);
        public static readonly Color Sidebar = new(0.067f, 0.067f, 0.067f);
        public static readonly Color Panel = new(0.092f, 0.098f, 0.105f);
        public static readonly Color PanelInset = new(0.071f, 0.075f, 0.082f);
        public static readonly Color Composer = new(0.155f, 0.155f, 0.155f);
        public static readonly Color Input = new(0.057f, 0.061f, 0.067f);
        public static readonly Color InputHover = new(0.075f, 0.080f, 0.088f);
        public static readonly Color Popup = new(0.075f, 0.078f, 0.084f);
        public static readonly Color Hover = new(1f, 1f, 1f, 0.065f);
        public static readonly Color Border = new(0.18f, 0.19f, 0.21f);
        public static readonly Color BorderStrong = new(0.27f, 0.28f, 0.30f);
        public static readonly Color Text = new(0.91f, 0.91f, 0.91f);
        public static readonly Color Muted = new(0.58f, 0.59f, 0.62f);
        public static readonly Color Placeholder = new(0.43f, 0.44f, 0.47f);
        public static readonly Color Accent = new(0.96f, 0.35f, 0.12f);
        public static readonly Color Focus = new(1f, 0.43f, 0.20f);
        public static readonly Color Send = new(0.94f, 0.94f, 0.94f);
        public static readonly Color SendForeground = new(0.10f, 0.10f, 0.10f);
        public static readonly Color Danger = new(0.58f, 0.16f, 0.16f);
        public static readonly Color Selected = new(0.14f, 0.14f, 0.14f);
        public static readonly Color Transparent = new(0f, 0f, 0f, 0f);
        public static readonly Color UserMessage = new(0.16f, 0.16f, 0.17f);
        public static readonly Color AssistantMessage = new(0.075f, 0.080f, 0.087f);
        public static readonly Color ToolMessage = new(0.06f, 0.13f, 0.10f);
        public static readonly Color ErrorPanel = new(0.24f, 0.07f, 0.08f);
        public static readonly Color WarningPanel = new(0.23f, 0.16f, 0.05f);
        public static readonly Color Warning = new(0.95f, 0.65f, 0.18f);
        public static readonly Color Error = new(0.96f, 0.34f, 0.32f);

        public static AgentButton Button(string text, string tooltip, Action clicked, int width,
            Color? background = null, Color? foreground = null)
        {
            var button = new AgentButton(text, tooltip, clicked, background ?? Panel, foreground ?? Text);
            button.style.height = 32;
            button.style.flexShrink = 0;
            if (width > 0) button.style.width = width; else button.style.flexGrow = 1;
            button.style.marginLeft = 3;
            button.style.marginRight = 3;
            button.style.borderTopLeftRadius = 7;
            button.style.borderTopRightRadius = 7;
            button.style.borderBottomLeftRadius = 7;
            button.style.borderBottomRightRadius = 7;
            return button;
        }

        public static AgentButton IconButton(string text, string tooltip, Action clicked, int size,
            Color? background = null, Color? foreground = null)
        {
            var button = Button(text, tooltip, clicked, size, background, foreground);
            button.style.height = size;
            button.style.fontSize = 16;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            return button;
        }

        public static AgentTextField Field(string label, string value, string tooltip, bool password = false)
        {
            var field = new AgentTextField(label)
            {
                value = value,
                isPasswordField = password
            };
            AgentTooltip.Attach(field, tooltip);
            field.style.marginTop = 4;
            field.style.marginBottom = 4;
            field.style.minWidth = 0;
            field.style.maxWidth = new Length(100, LengthUnit.Percent);
            field.style.flexShrink = 1;
            return field;
        }

        public static AgentChoiceField Dropdown(string label, IEnumerable<string> choices)
        {
            var list = choices.ToList();
            var field = new AgentChoiceField(label, list);
            field.style.marginTop = 4;
            field.style.marginBottom = 4;
            field.style.minWidth = 0;
            field.style.maxWidth = new Length(100, LengthUnit.Percent);
            field.style.flexShrink = 1;
            if (list.Count > 0) field.SetValueWithoutNotify(list[0]);
            return field;
        }

        public static AgentChoiceField CompactDropdown(IEnumerable<string> choices, string tooltip)
        {
            var list = choices.ToList();
            var field = new AgentChoiceField(string.Empty, list, true);
            if (list.Count > 0) field.SetValueWithoutNotify(list[0]);
            AgentTooltip.Attach(field, tooltip);
            field.style.width = 120;
            field.style.flexGrow = 0;
            field.style.flexShrink = 0;
            field.style.marginLeft = 3;
            field.style.marginRight = 3;
            return field;
        }

        public static ScrollView Scroll(ScrollViewMode mode)
        {
            var scroll = new ScrollView(mode);
            scroll.style.backgroundImage = StyleKeyword.None;
            scroll.style.backgroundColor = Transparent;
            scroll.style.borderTopWidth = 0;
            scroll.style.borderRightWidth = 0;
            scroll.style.borderBottomWidth = 0;
            scroll.style.borderLeftWidth = 0;
            scroll.contentContainer.style.backgroundImage = StyleKeyword.None;
            scroll.contentContainer.style.backgroundColor = Transparent;
            scroll.contentViewport.style.backgroundImage = StyleKeyword.None;
            scroll.contentViewport.style.backgroundColor = Transparent;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.schedule.Execute(() => StyleScroller(scroll));
            return scroll;
        }

        public static void StyleScroller(ScrollView scroll)
        {
            var scroller = scroll.verticalScroller;
            scroller.style.width = 9;
            scroller.style.backgroundImage = StyleKeyword.None;
            scroller.style.backgroundColor = Transparent;
            scroller.style.marginTop = 2;
            scroller.style.marginRight = 1;
            scroller.style.marginBottom = 2;
            scroller.lowButton.style.display = DisplayStyle.None;
            scroller.highButton.style.display = DisplayStyle.None;
            scroller.lowButton.style.backgroundImage = StyleKeyword.None;
            scroller.highButton.style.backgroundImage = StyleKeyword.None;
            scroller.slider.style.backgroundImage = StyleKeyword.None;
            scroller.slider.style.backgroundColor = Transparent;
            scroller.slider.style.borderTopWidth = 0;
            scroller.slider.style.borderRightWidth = 0;
            scroller.slider.style.borderBottomWidth = 0;
            scroller.slider.style.borderLeftWidth = 0;
            var tracker = scroller.slider.Q<VisualElement>(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundImage = StyleKeyword.None;
                tracker.style.backgroundColor = Transparent;
                tracker.style.borderTopWidth = 0;
                tracker.style.borderRightWidth = 0;
                tracker.style.borderBottomWidth = 0;
                tracker.style.borderLeftWidth = 0;
            }
            var dragger = scroller.slider.Q<VisualElement>(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.backgroundImage = StyleKeyword.None;
                dragger.style.backgroundColor = BorderStrong;
                dragger.style.borderTopWidth = 0;
                dragger.style.borderRightWidth = 0;
                dragger.style.borderBottomWidth = 0;
                dragger.style.borderLeftWidth = 0;
                dragger.style.borderTopLeftRadius = 4;
                dragger.style.borderTopRightRadius = 4;
                dragger.style.borderBottomLeftRadius = 4;
                dragger.style.borderBottomRightRadius = 4;
                dragger.RegisterCallback<PointerEnterEvent>(_ => dragger.style.backgroundColor = Muted);
                dragger.RegisterCallback<PointerLeaveEvent>(_ => dragger.style.backgroundColor = BorderStrong);
                dragger.RegisterCallback<PointerDownEvent>(_ => dragger.style.backgroundColor = Focus);
                dragger.RegisterCallback<PointerUpEvent>(_ => dragger.style.backgroundColor = Muted);
            }
            var draggerBorder = scroller.slider.Q<VisualElement>(className: "unity-base-slider__dragger-border");
            if (draggerBorder != null)
            {
                draggerBorder.style.backgroundImage = StyleKeyword.None;
                draggerBorder.style.backgroundColor = Transparent;
                draggerBorder.style.borderTopWidth = 0;
                draggerBorder.style.borderRightWidth = 0;
                draggerBorder.style.borderBottomWidth = 0;
                draggerBorder.style.borderLeftWidth = 0;
            }
        }

        public static VisualElement Card(string title, string subtitle)
        {
            var card = RoundedPanel(14);
            card.style.minWidth = 0;
            card.style.width = new Length(100, LengthUnit.Percent);
            card.style.maxWidth = 1040;
            card.style.alignSelf = Align.Center;
            card.style.marginBottom = 12;
            card.style.paddingLeft = 15;
            card.style.paddingRight = 15;
            card.style.paddingTop = 13;
            card.style.paddingBottom = 15;
            card.style.backgroundColor = Panel;
            SetBorder(card, Border, 1);
            var heading = new Label(title);
            heading.style.fontSize = 16;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(heading);
            var help = new Label(subtitle);
            help.style.color = Muted;
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.marginTop = 2;
            help.style.marginBottom = 8;
            card.Add(help);
            return card;
        }

        public static VisualElement RoundedPanel(float radius)
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = Panel;
            panel.style.borderTopLeftRadius = radius;
            panel.style.borderTopRightRadius = radius;
            panel.style.borderBottomLeftRadius = radius;
            panel.style.borderBottomRightRadius = radius;
            return panel;
        }

        public static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }

        public static VisualElement Inset()
        {
            var inset = RoundedPanel(7);
            inset.style.minWidth = 0;
            inset.style.maxWidth = new Length(100, LengthUnit.Percent);
            inset.style.backgroundColor = PanelInset;
            inset.style.paddingLeft = 10;
            inset.style.paddingRight = 10;
            inset.style.paddingTop = 8;
            inset.style.paddingBottom = 8;
            return inset;
        }

        public static VisualElement WrapRow()
        {
            var row = new VisualElement();
            row.style.minWidth = 0;
            row.style.maxWidth = new Length(100, LengthUnit.Percent);
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            return row;
        }
    }
}
