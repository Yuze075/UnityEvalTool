#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YuzeToolkit.UnityAgent
{
    public enum AgentPermissionMode
    {
        FullAccess = 0,
        ConfirmWrites = 1
    }

    public enum AgentToolAccess
    {
        Write = 0,
        ReadOnly = 1
    }

    public enum AgentMessageRole
    {
        User,
        Assistant,
        Tool
    }

    public enum AgentSessionState
    {
        Idle,
        Running,
        AwaitingApproval,
        Completed,
        Interrupted,
        Failed,
        // Kept only so schema V1/V2 histories can be repaired to Interrupted on load.
        StepLimitReached
    }

    public enum AgentStreamEventKind
    {
        RunStarted,
        TextDelta,
        ReasoningDelta,
        ToolCallStarted,
        ToolCallArgumentsDelta,
        UsageUpdated,
        RunCompleted,
        RunFailed
    }

    public sealed class AgentProviderProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string ProviderPresetId { get; set; } = "openai";

        public string Name { get; set; } = "OpenAI";

        public string Protocol { get; set; } = AgentProtocolIds.OpenAiResponses;

        public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

        public string Model { get; set; } = string.Empty;

        public string ReasoningEffort { get; set; } = string.Empty;

        public string SecretEnvironmentVariable { get; set; } = "OPENAI_API_KEY";

        public int MaxOutputTokens { get; set; } = 4096;

        public bool StrictTools { get; set; } = true;
    }

    public static class AgentProtocolIds
    {
        public const string OpenAiResponses = "openai-responses";
        public const string OpenAiChat = "openai-chat";
        public const string AnthropicMessages = "anthropic-messages";
        public const string CodexAppServer = "codex-app-server";
        public const string GoogleGeminiInteractions = "google-gemini-interactions";

        public static readonly IReadOnlyList<string> All = new[]
        {
            OpenAiResponses,
            OpenAiChat,
            AnthropicMessages,
            CodexAppServer,
            GoogleGeminiInteractions
        };
    }

    /// <summary>
    /// Stable, machine-independent anchors used by every configurable Agent path.
    /// The value is persisted by name; do not reorder or rename existing members.
    /// </summary>
    public enum AgentPathBase
    {
        ProjectRoot = 0,
        PersistentData = 1,
        UserProfile = 2,
        Documents = 3,
        LocalApplicationData = 4,
        RoamingApplicationData = 5,
        TemporaryCache = 6,
        StreamingAssets = 7
    }

    /// <summary>
    /// A portable path made from a stable base and an optional relative path.
    /// RelativePath may contain parent segments, but it must never be absolute.
    /// </summary>
    public sealed class AgentPathLocation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public AgentPathBase BasePath { get; set; } = AgentPathBase.ProjectRoot;

        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// When true, this instruction root is copied into Player StreamingAssets. Editor discovery
        /// always follows the ordered list regardless of this flag.
        /// </summary>
        public bool IncludeInPlayerBuild { get; set; }

        public static AgentPathLocation ProjectAgentsRoot() => new()
        {
            Id = "project-agents",
            BasePath = AgentPathBase.ProjectRoot,
            RelativePath = string.Empty,
            IncludeInPlayerBuild = true
        };

        public static AgentPathLocation ProjectSkillsRoot() => new()
        {
            Id = "project-skills",
            BasePath = AgentPathBase.ProjectRoot,
            RelativePath = ".agents/skills",
            IncludeInPlayerBuild = true
        };

        public static AgentPathLocation PersistentAgentsRoot() => new()
        {
            Id = "persistent-agents",
            BasePath = AgentPathBase.PersistentData,
            RelativePath = string.Empty,
            IncludeInPlayerBuild = false
        };

        public static AgentPathLocation PersistentSkillsRoot() => new()
        {
            Id = "persistent-skills",
            BasePath = AgentPathBase.PersistentData,
            RelativePath = ".agents/skills",
            IncludeInPlayerBuild = false
        };
    }

    public sealed class AgentSettingsDocument
    {
        public const int CurrentSchemaVersion = 3;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string DefaultProviderProfileId { get; set; } = string.Empty;

        public AgentPermissionMode PermissionMode { get; set; } = AgentPermissionMode.FullAccess;

        public string EditorSystemPrompt { get; set; } = AgentPromptDefaults.EditorSystemPrompt;

        public string RuntimeSystemPrompt { get; set; } = AgentPromptDefaults.RuntimeSystemPrompt;

        public int DefaultToolTimeoutSeconds { get; set; } = 120;

        public List<AgentProviderProfile> ProviderProfiles { get; set; } = new();

        /// <summary>Ordered, highest-priority-first AGENTS.md discovery roots.</summary>
        public List<AgentPathLocation> AgentsRoots { get; set; } = new();

        /// <summary>Ordered, highest-priority-first directories containing Skills.</summary>
        public List<AgentPathLocation> SkillRoots { get; set; } = new();

        public static AgentSettingsDocument CreateDefault()
        {
            var profile = new AgentProviderProfile();
            if (!AgentProviderCatalog.ApplyPreset(profile, "openai"))
                throw new InvalidOperationException("The built-in OpenAI Provider preset is missing.");
            return new AgentSettingsDocument
            {
                DefaultProviderProfileId = profile.Id,
                ProviderProfiles = new List<AgentProviderProfile> { profile },
                AgentsRoots = new List<AgentPathLocation>
                {
                    AgentPathLocation.ProjectAgentsRoot(),
                    AgentPathLocation.PersistentAgentsRoot()
                },
                SkillRoots = new List<AgentPathLocation>
                {
                    AgentPathLocation.ProjectSkillsRoot(),
                    AgentPathLocation.PersistentSkillsRoot()
                }
            };
        }
    }

    /// <summary>
    /// Provider-free defaults stored with the Unity project and included in Player builds.
    /// Machine credentials and Provider endpoints are intentionally absent.
    /// </summary>
    public sealed class AgentProjectSettingsDocument
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public AgentPermissionMode PermissionMode { get; set; } = AgentPermissionMode.FullAccess;
        public string EditorSystemPrompt { get; set; } = AgentPromptDefaults.EditorSystemPrompt;
        public string RuntimeSystemPrompt { get; set; } = AgentPromptDefaults.RuntimeSystemPrompt;
        public int DefaultToolTimeoutSeconds { get; set; } = 120;
        public List<AgentPathLocation> AgentsRoots { get; set; } = new()
        {
            AgentPathLocation.ProjectAgentsRoot(), AgentPathLocation.PersistentAgentsRoot()
        };
        public List<AgentPathLocation> SkillRoots { get; set; } = new()
        {
            AgentPathLocation.ProjectSkillsRoot(), AgentPathLocation.PersistentSkillsRoot()
        };

        public static AgentProjectSettingsDocument FromSettings(AgentSettingsDocument settings) => new()
        {
            PermissionMode = settings.PermissionMode,
            EditorSystemPrompt = settings.EditorSystemPrompt,
            RuntimeSystemPrompt = settings.RuntimeSystemPrompt,
            DefaultToolTimeoutSeconds = settings.DefaultToolTimeoutSeconds,
            AgentsRoots = settings.AgentsRoots.Select(ClonePath).ToList(),
            SkillRoots = settings.SkillRoots.Select(ClonePath).ToList()
        };

        public void ApplyTo(AgentSettingsDocument settings)
        {
            settings.PermissionMode = PermissionMode;
            settings.EditorSystemPrompt = EditorSystemPrompt;
            settings.RuntimeSystemPrompt = RuntimeSystemPrompt;
            settings.DefaultToolTimeoutSeconds = Math.Max(1, DefaultToolTimeoutSeconds);
            settings.AgentsRoots = AgentsRoots.Select(ClonePath).ToList();
            settings.SkillRoots = SkillRoots.Select(ClonePath).ToList();
        }

        private static AgentPathLocation ClonePath(AgentPathLocation value) => new()
        {
            Id = value.Id,
            BasePath = value.BasePath,
            RelativePath = value.RelativePath,
            IncludeInPlayerBuild = value.IncludeInPlayerBuild
        };
    }

    public static class AgentPromptDefaults
    {
        public const string EditorSystemPrompt =
            "You are a Unity Editor development agent running inside the current Unity Editor process. " +
            "Work autonomously through multiple tool calls until the user's task is complete. " +
            "Inspect relevant files and Unity state before changing them, preserve unrelated work, " +
            "report tool failures honestly, and never claim an action succeeded without its tool result. " +
            "Use unity_eval_js for native Unity and IEvalTool operations. Use file and process tools for host operations.";

        public const string RuntimeSystemPrompt =
            "You are a runtime Unity game agent embedded in the currently running Player. " +
            "Operate only through tools available in this build, prioritize observing and controlling live game state, " +
            "do not assume UnityEditor APIs or project source files exist, and report unavailable operations explicitly. " +
            "Continue through multiple safe tool calls until the user's runtime task is complete.";
    }

    public sealed class AgentToolCall
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string ArgumentsJson { get; set; } = "{}";

        public string ProviderItemId { get; set; } = string.Empty;
    }

    public sealed class AgentMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public AgentMessageRole Role { get; set; }

        public string Text { get; set; } = string.Empty;

        public List<AgentToolCall> ToolCalls { get; set; } = new();

        public string ToolCallId { get; set; } = string.Empty;

        public string ToolName { get; set; } = string.Empty;

        public bool IsError { get; set; }

        /// <summary>
        /// Provider-owned JSON that must round-trip across tool turns. The host persists but
        /// never interprets this value.
        /// </summary>
        public string ProviderDataJson { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class AgentUsage
    {
        public long InputTokens { get; set; }

        public long OutputTokens { get; set; }

        public long TotalTokens => InputTokens + OutputTokens;
    }

    public sealed class AgentSessionDocument
    {
        public const int CurrentSchemaVersion = 4;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Title { get; set; } = "New conversation";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public string ProviderProfileId { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string ReasoningEffort { get; set; } = string.Empty;

        public AgentPermissionMode PermissionMode { get; set; } = AgentPermissionMode.FullAccess;

        public string SystemPrompt { get; set; } = string.Empty;

        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Opaque conversation identifier owned by a stateful backend such as Codex App Server.
        /// HTTP model protocols leave this empty.
        /// </summary>
        public string ProviderThreadId { get; set; } = string.Empty;

        public AgentSessionState State { get; set; } = AgentSessionState.Idle;

        public List<AgentMessage> Messages { get; set; } = new();

        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Total number of messages physically removed from Messages and represented by Summary.
        /// Schema V3 never keeps the summarized prefix in Messages.
        /// </summary>
        public int SummarizedMessageCount { get; set; }

        public int CompletedSteps { get; set; }

        public AgentUsage Usage { get; set; } = new();

        public string LastError { get; set; } = string.Empty;

        public AgentApprovalRequest? PendingApproval { get; set; }

        public bool IsPinned { get; set; }

        public bool IsArchived { get; set; }

        public int SortOrder { get; set; }

        /// <summary>Independent unsent composer text for this persisted conversation.</summary>
        public string Draft { get; set; } = string.Empty;
    }

    public sealed class AgentToolDescriptor
    {
        public AgentToolDescriptor(
            string name,
            string description,
            AgentToolAccess access,
            Dictionary<string, object?> parameters)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tool name is required.", nameof(name));
            Name = name;
            Description = description ?? string.Empty;
            Access = access;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        public string Name { get; }

        public string Description { get; }

        public AgentToolAccess Access { get; }

        public Dictionary<string, object?> Parameters { get; }
    }

    public sealed class AgentToolResult
    {
        public bool IsError { get; set; }

        public string Text { get; set; } = string.Empty;

        public static AgentToolResult Success(string text) => new() { Text = text ?? string.Empty };

        public static AgentToolResult Error(string text) => new() { IsError = true, Text = text ?? string.Empty };
    }

    public sealed class AgentToolContext
    {
        internal AgentToolContext(string sessionId, string workingDirectory, int defaultTimeoutSeconds)
        {
            SessionId = sessionId;
            WorkingDirectory = workingDirectory;
            DefaultTimeoutSeconds = Math.Max(1, defaultTimeoutSeconds);
        }

        public string SessionId { get; }

        public string WorkingDirectory { get; }

        public int DefaultTimeoutSeconds { get; }
    }

    public interface IAgentTool
    {
        AgentToolDescriptor Descriptor { get; }

        Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken);
    }

    public sealed class AgentModelRequest
    {
        public string SessionId { get; set; } = string.Empty;

        public string ProviderThreadId { get; set; } = string.Empty;

        public string WorkingDirectory { get; set; } = string.Empty;

        public AgentPermissionMode PermissionMode { get; set; } = AgentPermissionMode.FullAccess;

        public int DefaultToolTimeoutSeconds { get; set; } = 120;

        public string SystemPrompt { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string ReasoningEffort { get; set; } = string.Empty;

        public int MaxOutputTokens { get; set; }

        public IReadOnlyList<AgentMessage> Messages { get; set; } = Array.Empty<AgentMessage>();

        public IReadOnlyList<AgentToolDescriptor> Tools { get; set; } = Array.Empty<AgentToolDescriptor>();
    }

    public sealed class AgentModelResponse
    {
        public string Text { get; set; } = string.Empty;

        public string ProviderThreadId { get; set; } = string.Empty;

        public string ProviderDataJson { get; set; } = string.Empty;

        public List<AgentToolCall> ToolCalls { get; set; } = new();

        public AgentUsage Usage { get; set; } = new();

        public string FinishReason { get; set; } = string.Empty;
    }

    public sealed class AgentStreamEvent
    {
        public AgentStreamEvent(AgentStreamEventKind kind, string text = "", string callId = "")
        {
            Kind = kind;
            Text = text ?? string.Empty;
            CallId = callId ?? string.Empty;
        }

        public AgentStreamEventKind Kind { get; }

        public string Text { get; }

        public string CallId { get; }
    }

    public interface IAgentModelProvider
    {
        Task<AgentModelResponse> CompleteAsync(
            AgentProviderProfile profile,
            AgentModelRequest request,
            Action<AgentStreamEvent>? onEvent,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> ListModelsAsync(
            AgentProviderProfile profile,
            CancellationToken cancellationToken);
    }

    public sealed class AgentCodexAccountStatus
    {
        public bool IsSignedIn { get; set; }

        public bool RequiresOpenAiAuth { get; set; }

        public string AccountType { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PlanType { get; set; } = string.Empty;
    }

    public sealed class AgentCodexLogin
    {
        public string LoginId { get; set; } = string.Empty;

        public string AuthorizationUrl { get; set; } = string.Empty;

        public string UserCode { get; set; } = string.Empty;
    }
}
