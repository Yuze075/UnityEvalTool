#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace YuzeToolkit.UnityAgent
{
    internal static class AgentDocumentCodec
    {
        public static string SerializeSettings(AgentSettingsDocument settings) =>
            AgentJson.Stringify(ToJson(settings));

        public static AgentSettingsDocument DeserializeSettings(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Agent settings JSON is empty.");
            var root = AgentJson.ParseObject(json);
            var sourceSchemaVersion = AgentJson.GetSchemaVersion(root);
            if (sourceSchemaVersion > AgentSettingsDocument.CurrentSchemaVersion)
            {
                throw new FormatException(
                    $"Settings schema version {sourceSchemaVersion} is newer than the supported version " +
                    $"{AgentSettingsDocument.CurrentSchemaVersion}.");
            }
            var settings = new AgentSettingsDocument
            {
                SchemaVersion = sourceSchemaVersion,
                DefaultProviderProfileId = AgentJson.GetString(root, "defaultProviderProfileId"),
                PermissionMode = AgentJson.GetEnum(root, "permissionMode", AgentPermissionMode.FullAccess),
                EditorSystemPrompt = AgentJson.GetString(root, "editorSystemPrompt",
                    AgentJson.GetString(root, "systemPrompt", AgentPromptDefaults.EditorSystemPrompt)),
                RuntimeSystemPrompt = AgentJson.GetString(root, "runtimeSystemPrompt",
                    sourceSchemaVersion < 3
                        ? AgentJson.GetString(root, "systemPrompt", AgentPromptDefaults.RuntimeSystemPrompt)
                        : AgentPromptDefaults.RuntimeSystemPrompt),
                DefaultToolTimeoutSeconds = Math.Max(1, EvalData.GetInt(root, "defaultToolTimeoutSeconds", 120)),
                MaximumAgentSteps = Math.Max(1, EvalData.GetInt(root, "maximumAgentSteps", 64))
            };

            if (AgentPromptDefaults.IsPreviousEditorPrompt(settings.EditorSystemPrompt))
                settings.EditorSystemPrompt = AgentPromptDefaults.EditorSystemPrompt;
            if (AgentPromptDefaults.IsPreviousRuntimePrompt(settings.RuntimeSystemPrompt))
                settings.RuntimeSystemPrompt = AgentPromptDefaults.RuntimeSystemPrompt;

            foreach (var value in AgentJson.GetObjectArray(root, "providerProfiles"))
                settings.ProviderProfiles.Add(ReadProviderProfile(value));

            if (root.ContainsKey("agentsRoots"))
            {
                foreach (var value in AgentJson.GetObjectArray(root, "agentsRoots"))
                    settings.AgentsRoots.Add(ReadPathLocation(value));
            }
            else
            {
                settings.AgentsRoots.Add(AgentPathLocation.ProjectAgentsRoot());
                ReadLegacyContentRoots(root, settings, includeAgents: true);
            }

            if (root.ContainsKey("skillRoots"))
            {
                foreach (var value in AgentJson.GetObjectArray(root, "skillRoots"))
                    settings.SkillRoots.Add(ReadPathLocation(value));
            }
            else
            {
                settings.SkillRoots.Add(AgentPathLocation.ProjectSkillsRoot());
                ReadLegacyContentRoots(root, settings, includeAgents: false);
            }

            if (sourceSchemaVersion < 3)
            {
                EnsureDefaultRoot(settings.AgentsRoots, AgentPathLocation.PersistentAgentsRoot());
                EnsureDefaultRoot(settings.SkillRoots, AgentPathLocation.PersistentSkillsRoot());
            }

            if (settings.ProviderProfiles.Count == 0)
            {
                var defaultProfile = new AgentProviderProfile();
                if (!AgentProviderCatalog.ApplyPreset(defaultProfile, "openai"))
                    throw new InvalidOperationException("The built-in OpenAI Provider preset is missing.");
                settings.ProviderProfiles.Add(defaultProfile);
                settings.DefaultProviderProfileId = defaultProfile.Id;
            }
            if (string.IsNullOrWhiteSpace(settings.DefaultProviderProfileId) ||
                settings.ProviderProfiles.All(profile => profile.Id != settings.DefaultProviderProfileId))
                settings.DefaultProviderProfileId = settings.ProviderProfiles[0].Id;
            settings.SchemaVersion = AgentSettingsDocument.CurrentSchemaVersion;
            return settings;
        }

        public static string SerializeSession(AgentSessionDocument session) =>
            AgentJson.Stringify(ToJson(session));

        public static AgentSessionDocument DeserializeSession(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Agent session JSON is empty.");
            var root = AgentJson.ParseObject(json);
            var sourceSchemaVersion = AgentJson.GetSchemaVersion(root);
            if (sourceSchemaVersion > AgentSessionDocument.CurrentSchemaVersion)
            {
                throw new FormatException(
                    $"Session schema version {sourceSchemaVersion} is newer than the supported version " +
                    $"{AgentSessionDocument.CurrentSchemaVersion}.");
            }
            var sessionId = AgentJson.GetString(root, "id");
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new FormatException("Agent session JSON property 'id' is required.");
            var session = new AgentSessionDocument
            {
                SchemaVersion = sourceSchemaVersion,
                Id = sessionId,
                Title = AgentJson.GetString(root, "title", "New conversation"),
                CreatedAtUtc = AgentJson.GetDateTime(root, "createdAtUtc", DateTime.UtcNow),
                UpdatedAtUtc = AgentJson.GetDateTime(root, "updatedAtUtc", DateTime.UtcNow),
                ProviderProfileId = AgentJson.GetString(root, "providerProfileId"),
                Model = AgentJson.GetString(root, "model"),
                ReasoningEffort = AgentJson.GetString(root, "reasoningEffort"),
                PermissionMode = AgentJson.GetEnum(root, "permissionMode", AgentPermissionMode.FullAccess),
                SystemPrompt = AgentJson.GetString(root, "systemPrompt"),
                WorkingDirectory = AgentJson.GetString(root, "workingDirectory"),
                ProviderThreadId = AgentJson.GetString(root, "providerThreadId"),
                State = AgentJson.GetEnum(root, "state", AgentSessionState.Idle),
                Summary = AgentJson.GetString(root, "summary"),
                SummarizedMessageCount = Math.Max(0, EvalData.GetInt(root, "summarizedMessageCount")),
                ContextSummaryMessageCount = Math.Max(0,
                    EvalData.GetInt(root, "contextSummaryMessageCount")),
                CompletedSteps = Math.Max(0, EvalData.GetInt(root, "completedSteps")),
                LastError = AgentJson.GetString(root, "lastError"),
                IsPinned = EvalData.GetBool(root, "isPinned"),
                IsArchived = EvalData.GetBool(root, "isArchived"),
                SortOrder = EvalData.GetInt(root, "sortOrder"),
                Draft = AgentJson.GetString(root, "draft")
            };

            foreach (var value in AgentJson.GetObjectArray(root, "messages"))
                session.Messages.Add(ReadMessage(value));
            session.ContextSummaryMessageCount = Math.Min(session.ContextSummaryMessageCount,
                session.Messages.Count);

            // Schema V1/V2 retained both the summarized prefix and the summary. V3 makes the
            // summary authoritative and physically removes that prefix so histories remain bounded.
            // This exactly preserves the old ProjectMessages projection (summary + unsummarized tail).
            if (sourceSchemaVersion < 3 && session.SummarizedMessageCount > 0 &&
                !string.IsNullOrWhiteSpace(session.Summary))
            {
                var summarizedPrefix = Math.Min(session.SummarizedMessageCount, session.Messages.Count);
                if (summarizedPrefix > 0) session.Messages.RemoveRange(0, summarizedPrefix);
            }

            if (AgentJson.GetOptionalObject(root, "usage") is { } usage)
            {
                session.Usage.InputTokens = AgentJson.GetLong(usage, "inputTokens");
                session.Usage.OutputTokens = AgentJson.GetLong(usage, "outputTokens");
            }

            if (AgentJson.GetOptionalObject(root, "pendingApproval") is { } approval)
                session.PendingApproval = ReadApproval(approval);
            session.SchemaVersion = AgentSessionDocument.CurrentSchemaVersion;
            return session;
        }

        public static AgentSessionDocument Clone(AgentSessionDocument session) =>
            DeserializeSession(SerializeSession(session));

        public static AgentSettingsDocument Clone(AgentSettingsDocument settings) =>
            DeserializeSettings(SerializeSettings(settings));

        public static string SerializeProjectSettings(AgentProjectSettingsDocument settings) =>
            AgentJson.Stringify(AgentJson.Object(
                ("schemaVersion", AgentProjectSettingsDocument.CurrentSchemaVersion),
                ("permissionMode", settings.PermissionMode.ToString()),
                ("editorSystemPrompt", settings.EditorSystemPrompt),
                ("runtimeSystemPrompt", settings.RuntimeSystemPrompt),
                ("defaultToolTimeoutSeconds", settings.DefaultToolTimeoutSeconds),
                ("maximumAgentSteps", settings.MaximumAgentSteps),
                ("agentsRoots", settings.AgentsRoots.Select(ToJson).Cast<object?>().ToList()),
                ("skillRoots", settings.SkillRoots.Select(ToJson).Cast<object?>().ToList())));

        public static AgentProjectSettingsDocument DeserializeProjectSettings(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Unity Agent Project Settings JSON is empty.");
            var root = AgentJson.ParseObject(json);
            var version = AgentJson.GetSchemaVersion(root);
            if (version > AgentProjectSettingsDocument.CurrentSchemaVersion)
                throw new FormatException(
                    $"Project Settings schema version {version} is newer than the supported version " +
                    $"{AgentProjectSettingsDocument.CurrentSchemaVersion}.");
            var result = new AgentProjectSettingsDocument
            {
                SchemaVersion = AgentProjectSettingsDocument.CurrentSchemaVersion,
                PermissionMode = AgentJson.GetEnum(root, "permissionMode", AgentPermissionMode.FullAccess),
                EditorSystemPrompt = AgentJson.GetString(root, "editorSystemPrompt",
                    AgentPromptDefaults.EditorSystemPrompt),
                RuntimeSystemPrompt = AgentJson.GetString(root, "runtimeSystemPrompt",
                    AgentPromptDefaults.RuntimeSystemPrompt),
                DefaultToolTimeoutSeconds = Math.Max(1,
                    EvalData.GetInt(root, "defaultToolTimeoutSeconds", 120)),
                MaximumAgentSteps = Math.Max(1, EvalData.GetInt(root, "maximumAgentSteps", 64)),
                AgentsRoots = AgentJson.GetObjectArray(root, "agentsRoots").Select(ReadPathLocation).ToList(),
                SkillRoots = AgentJson.GetObjectArray(root, "skillRoots").Select(ReadPathLocation).ToList()
            };
            if (AgentPromptDefaults.IsPreviousEditorPrompt(result.EditorSystemPrompt))
                result.EditorSystemPrompt = AgentPromptDefaults.EditorSystemPrompt;
            if (AgentPromptDefaults.IsPreviousRuntimePrompt(result.RuntimeSystemPrompt))
                result.RuntimeSystemPrompt = AgentPromptDefaults.RuntimeSystemPrompt;
            return result;
        }

        private static Dictionary<string, object?> ToJson(AgentSettingsDocument settings)
        {
            return AgentJson.Object(
                ("schemaVersion", AgentSettingsDocument.CurrentSchemaVersion),
                ("defaultProviderProfileId", settings.DefaultProviderProfileId),
                ("permissionMode", settings.PermissionMode.ToString()),
                ("editorSystemPrompt", settings.EditorSystemPrompt),
                ("runtimeSystemPrompt", settings.RuntimeSystemPrompt),
                ("defaultToolTimeoutSeconds", settings.DefaultToolTimeoutSeconds),
                ("maximumAgentSteps", settings.MaximumAgentSteps),
                ("providerProfiles", settings.ProviderProfiles.Select(ToJson).Cast<object?>().ToList()),
                ("agentsRoots", settings.AgentsRoots.Select(ToJson).Cast<object?>().ToList()),
                ("skillRoots", settings.SkillRoots.Select(ToJson).Cast<object?>().ToList()));
        }

        private static Dictionary<string, object?> ToJson(AgentProviderProfile profile)
        {
            return AgentJson.Object(
                ("id", profile.Id),
                ("providerPresetId", profile.ProviderPresetId),
                ("name", profile.Name),
                ("protocol", profile.Protocol),
                ("baseUrl", profile.BaseUrl),
                ("model", profile.Model),
                ("reasoningEffort", profile.ReasoningEffort),
                ("secretEnvironmentVariable", profile.SecretEnvironmentVariable),
                ("maxOutputTokens", profile.MaxOutputTokens),
                ("contextWindowTokens", profile.ContextWindowTokens),
                ("strictTools", profile.StrictTools));
        }

        private static AgentProviderProfile ReadProviderProfile(Dictionary<string, object?> value)
        {
            var protocol = AgentJson.GetString(value, "protocol", AgentProtocolIds.OpenAiResponses);
            var baseUrl = AgentJson.GetString(value, "baseUrl", "https://api.openai.com/v1/");
            var persistedPresetId = AgentJson.GetString(value, "providerPresetId");
            var presetId = persistedPresetId;
            if (string.IsNullOrWhiteSpace(persistedPresetId))
                presetId = InferProviderPresetId(protocol, baseUrl);
            var profile = new AgentProviderProfile
            {
                Id = AgentJson.GetString(value, "id", Guid.NewGuid().ToString("N")),
                ProviderPresetId = presetId,
                Name = AgentJson.GetString(value, "name", "Provider"),
                Protocol = protocol,
                BaseUrl = baseUrl,
                Model = AgentJson.GetString(value, "model"),
                ReasoningEffort = AgentJson.GetString(value, "reasoningEffort"),
                SecretEnvironmentVariable = AgentJson.GetString(value, "secretEnvironmentVariable"),
                MaxOutputTokens = Math.Max(1, EvalData.GetInt(value, "maxOutputTokens", 4096)),
                ContextWindowTokens = Math.Max(8_192,
                    EvalData.GetInt(value, "contextWindowTokens", 128_000)),
                StrictTools = EvalData.GetBool(value, "strictTools", true)
            };
            // V1 profiles had no preset id and were commonly materialized with an empty model.
            // Upgrade only that legacy shape to a directly usable curated default. Explicit V2
            // empty model values remain untouched so custom endpoints can still defer selection.
            if (string.IsNullOrWhiteSpace(persistedPresetId) &&
                string.IsNullOrWhiteSpace(profile.Model) &&
                !string.Equals(presetId, "custom", StringComparison.OrdinalIgnoreCase))
                AgentProviderCatalog.ApplyPreset(profile, presetId);
            return profile;
        }

        private static string InferProviderPresetId(string protocol, string baseUrl)
        {
            if (string.Equals(protocol, AgentProtocolIds.CodexAppServer, StringComparison.Ordinal))
                return "openai-codex";
            foreach (var preset in AgentProviderCatalog.Providers)
            {
                if (string.Equals(preset.Protocol, protocol, StringComparison.Ordinal) &&
                    string.Equals(preset.BaseUrl.TrimEnd('/'), (baseUrl ?? string.Empty).TrimEnd('/'),
                        StringComparison.OrdinalIgnoreCase))
                    return preset.Id;
            }
            return "custom";
        }

        private static Dictionary<string, object?> ToJson(AgentPathLocation location)
        {
            return AgentJson.Object(
                ("id", location.Id),
                ("basePath", location.BasePath.ToString()),
                ("relativePath", location.RelativePath),
                ("includeInPlayerBuild", location.IncludeInPlayerBuild));
        }

        private static AgentPathLocation ReadPathLocation(Dictionary<string, object?> value)
        {
            var location = new AgentPathLocation
            {
                Id = AgentJson.GetString(value, "id", Guid.NewGuid().ToString("N")),
                BasePath = AgentJson.GetEnum(value, "basePath", AgentPathBase.ProjectRoot),
                RelativePath = AgentJson.GetString(value, "relativePath"),
                IncludeInPlayerBuild = EvalData.GetBool(value, "includeInPlayerBuild")
            };
            AgentPaths.Validate(location);
            return location;
        }

        private static Dictionary<string, object?> ToJson(AgentSessionDocument session)
        {
            return AgentJson.Object(
                ("schemaVersion", AgentSessionDocument.CurrentSchemaVersion),
                ("id", session.Id),
                ("title", session.Title),
                ("createdAtUtc", AgentJson.Utc(session.CreatedAtUtc)),
                ("updatedAtUtc", AgentJson.Utc(session.UpdatedAtUtc)),
                ("providerProfileId", session.ProviderProfileId),
                ("model", session.Model),
                ("reasoningEffort", session.ReasoningEffort),
                ("permissionMode", session.PermissionMode.ToString()),
                ("systemPrompt", session.SystemPrompt),
                ("workingDirectory", session.WorkingDirectory),
                ("providerThreadId", session.ProviderThreadId),
                ("state", session.State.ToString()),
                ("messages", session.Messages.Select(ToJson).Cast<object?>().ToList()),
                ("summary", session.Summary),
                ("summarizedMessageCount", session.SummarizedMessageCount),
                ("contextSummaryMessageCount", session.ContextSummaryMessageCount),
                ("completedSteps", session.CompletedSteps),
                ("usage", AgentJson.Object(
                    ("inputTokens", session.Usage.InputTokens),
                    ("outputTokens", session.Usage.OutputTokens))),
                ("lastError", session.LastError),
                ("pendingApproval", session.PendingApproval == null ? null : ToJson(session.PendingApproval)),
                ("isPinned", session.IsPinned),
                ("isArchived", session.IsArchived),
                ("sortOrder", session.SortOrder),
                ("draft", session.Draft));
        }

        private static Dictionary<string, object?> ToJson(AgentMessage message)
        {
            return AgentJson.Object(
                ("id", message.Id),
                ("role", message.Role.ToString()),
                ("text", message.Text),
                ("toolCalls", message.ToolCalls.Select(ToJson).Cast<object?>().ToList()),
                ("toolCallId", message.ToolCallId),
                ("toolName", message.ToolName),
                ("isError", message.IsError),
                ("providerDataJson", message.ProviderDataJson),
                ("createdAtUtc", AgentJson.Utc(message.CreatedAtUtc)));
        }

        private static AgentMessage ReadMessage(Dictionary<string, object?> value)
        {
            var message = new AgentMessage
            {
                Id = AgentJson.GetString(value, "id", Guid.NewGuid().ToString("N")),
                Role = AgentJson.GetEnum(value, "role", AgentMessageRole.User),
                Text = AgentJson.GetString(value, "text"),
                ToolCallId = AgentJson.GetString(value, "toolCallId"),
                ToolName = AgentJson.GetString(value, "toolName"),
                IsError = EvalData.GetBool(value, "isError"),
                ProviderDataJson = AgentJson.GetString(value, "providerDataJson"),
                CreatedAtUtc = AgentJson.GetDateTime(value, "createdAtUtc", DateTime.UtcNow)
            };
            foreach (var call in AgentJson.GetObjectArray(value, "toolCalls"))
                message.ToolCalls.Add(ReadToolCall(call));
            return message;
        }

        private static Dictionary<string, object?> ToJson(AgentToolCall call)
        {
            return AgentJson.Object(
                ("id", call.Id),
                ("name", call.Name),
                ("argumentsJson", call.ArgumentsJson),
                ("providerItemId", call.ProviderItemId));
        }

        private static AgentToolCall ReadToolCall(Dictionary<string, object?> value)
        {
            return new AgentToolCall
            {
                Id = AgentJson.GetString(value, "id"),
                Name = AgentJson.GetString(value, "name"),
                ArgumentsJson = AgentJson.GetString(value, "argumentsJson", "{}"),
                ProviderItemId = AgentJson.GetString(value, "providerItemId")
            };
        }

        private static Dictionary<string, object?> ToJson(AgentApprovalRequest approval)
        {
            return AgentJson.Object(
                ("id", approval.Id),
                ("sessionId", approval.SessionId),
                ("toolCallId", approval.ToolCallId),
                ("toolName", approval.ToolName),
                ("argumentsJson", approval.ArgumentsJson),
                ("description", approval.Description),
                ("createdAtUtc", AgentJson.Utc(approval.CreatedAtUtc)));
        }

        private static AgentApprovalRequest ReadApproval(Dictionary<string, object?> value)
        {
            return new AgentApprovalRequest
            {
                Id = AgentJson.GetString(value, "id", Guid.NewGuid().ToString("N")),
                SessionId = AgentJson.GetString(value, "sessionId"),
                ToolCallId = AgentJson.GetString(value, "toolCallId"),
                ToolName = AgentJson.GetString(value, "toolName"),
                ArgumentsJson = AgentJson.GetString(value, "argumentsJson", "{}"),
                Description = AgentJson.GetString(value, "description"),
                CreatedAtUtc = AgentJson.GetDateTime(value, "createdAtUtc", DateTime.UtcNow)
            };
        }

        private static void ReadLegacyContentRoots(
            Dictionary<string, object?> root,
            AgentSettingsDocument settings,
            bool includeAgents)
        {
            foreach (var value in AgentJson.GetObjectArray(root, "contentRoots"))
            {
                var include = includeAgents
                    ? EvalData.GetBool(value, "includeAgents", true)
                    : EvalData.GetBool(value, "includeSkills", true);
                if (!include) continue;
                var legacyId = AgentJson.GetString(value, "id", Guid.NewGuid().ToString("N"));
                var path = AgentJson.GetString(value, "path");
                var source = includeAgents
                    ? path
                    : PathCombineLegacy(path, ".agents/skills");
                var location = AgentPaths.FromLegacyPath(
                    (includeAgents ? "agents-" : "skills-") + legacyId, source);
                // V1's separate ProjectSettings build-selection asset cannot be represented reliably in
                // portable settings. Preserve the explicit project default, keep migrated external roots safe.
                location.IncludeInPlayerBuild = location.BasePath == AgentPathBase.ProjectRoot &&
                    (includeAgents
                        ? string.IsNullOrEmpty(location.RelativePath)
                        : location.RelativePath.Replace('\\', '/') == ".agents/skills");
                var list = includeAgents ? settings.AgentsRoots : settings.SkillRoots;
                if (list.Any(existing => AgentPaths.PathsEqual(AgentPaths.Resolve(existing), AgentPaths.Resolve(location))))
                    continue;
                list.Add(location);
            }
        }

        private static string PathCombineLegacy(string root, string child)
        {
            if (string.IsNullOrWhiteSpace(root)) return child;
            return System.IO.Path.Combine(root, child);
        }

        private static void EnsureDefaultRoot(List<AgentPathLocation> roots, AgentPathLocation required)
        {
            if (roots.Any(value => value.BasePath == required.BasePath &&
                                   string.Equals(value.RelativePath.Replace('\\', '/').TrimEnd('/'),
                                       required.RelativePath.Replace('\\', '/').TrimEnd('/'),
                                       StringComparison.Ordinal))) return;
            roots.Add(required);
        }
    }
}
