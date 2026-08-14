#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YuzeToolkit.UnityAgent
{
    /// <summary>
    /// Loads provider-free project defaults from a Resources TextAsset. The file is versioned with
    /// the Unity project and included in Player builds. Invalid or missing content deliberately
    /// falls back to the built-in defaults instead of preventing Agent startup.
    /// </summary>
    public static class UnityAgentProjectSettings
    {
        public const string ResourceName = "UnityAgentProjectSettings";

        public static AgentProjectSettingsDocument Load()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) return new AgentProjectSettingsDocument();
            try
            {
                var settings = AgentDocumentCodec.DeserializeProjectSettings(asset.text);
                Validate(settings);
                return settings;
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException or
                                               InvalidOperationException or OverflowException)
            {
                Debug.LogWarning(
                    $"Unity Agent Project Settings could not be parsed and built-in defaults will be used. " +
                    exception.Message);
                return new AgentProjectSettingsDocument();
            }
        }

        public static AgentSettingsDocument CreateMachineDefaults()
        {
            var settings = AgentSettingsDocument.CreateDefault();
            Load().ApplyTo(settings);
            return settings;
        }

        public static string Serialize(AgentSettingsDocument settings) =>
            AgentDocumentCodec.SerializeProjectSettings(AgentProjectSettingsDocument.FromSettings(settings));

        private static void Validate(AgentProjectSettingsDocument settings)
        {
            if (string.IsNullOrWhiteSpace(settings.EditorSystemPrompt) ||
                string.IsNullOrWhiteSpace(settings.RuntimeSystemPrompt))
                throw new FormatException("Editor and Runtime system prompts are required.");
            if (settings.DefaultToolTimeoutSeconds < 1)
                throw new FormatException("Default Tool timeout must be positive.");
            ValidateRoots(settings.AgentsRoots, "AGENTS.md");
            ValidateRoots(settings.SkillRoots, "Skill");
        }

        private static void ValidateRoots(IReadOnlyList<AgentPathLocation> roots, string name)
        {
            if (roots == null || roots.Count == 0)
                throw new FormatException($"{name} roots are required.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in roots)
            {
                AgentPaths.Validate(root, name + " root");
                if (!ids.Add(root.Id))
                    throw new FormatException($"Duplicate {name} root id '{root.Id}'.");
            }
        }
    }
}
